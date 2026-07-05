using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BarberShop.Application.Services;

public class AuthService : IAuthService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.AuthService");

    private static readonly Meter _meter =
        new("BarberShop.AuthService");

    private static readonly Counter<long> _loginSuccess =
        _meter.CreateCounter<long>(
            "barbershop.auth.login_success",
            description: "Total successful logins");

    private static readonly Counter<long> _loginFailed =
        _meter.CreateCounter<long>(
            "barbershop.auth.login_failed",
            description: "Total failed login attempts");

    private static readonly Counter<long> _accountsLocked =
        _meter.CreateCounter<long>(
            "barbershop.auth.accounts_locked",
            description: "Total accounts locked due to failed attempts");

    private static readonly Counter<long> _googleLogins =
        _meter.CreateCounter<long>(
            "barbershop.auth.google_login",
            description: "Total Google logins");

    private static readonly Counter<long> _accountsUnlocked =
        _meter.CreateCounter<long>(
            "barbershop.auth.accounts_unlocked",
            description: "Total accounts unlocked by admin");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly TokenService _token;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _config;

    public AuthService(
        IUnitOfWork uow,
        TokenService token,
        ILogger<AuthService> logger,
        IConfiguration config)
    {
        _uow = uow;
        _token = token;
        _logger = logger;
        _config = config;
    }

    // =========================
    // LOGIN
    // =========================
    public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto)
    {
        using var span = _activitySource.StartActivity("Login");

        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return Result<AuthResponseDTO>.Fail("Invalid credentials");

        // Normalize so "Joao@X.com" and "joao@x.com" resolve to the same account.
        var email = dto.Email.Trim().ToLowerInvariant();
        span?.SetTag("user.email", email);

        _logger.LogInformation("Login attempt for {Email}", email);

        var user = await _uow.Users.GetByEmailAsync(email);

        if (user == null || !user.IsActive)
        {
            _loginFailed.Add(1, new TagList { { "reason", "user_not_found_or_inactive" } });
            _logger.LogWarning("Login failed — user not found or inactive: {Email}", email);
            return Result<AuthResponseDTO>.Fail("Invalid credentials");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            _loginFailed.Add(1, new TagList { { "reason", "account_locked" } });
            _logger.LogWarning(
                "Login failed — account locked until {LockoutEnd}: {Email}",
                user.LockoutEnd, email);
            return Result<AuthResponseDTO>.Fail("Account is locked");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                _accountsLocked.Add(1);
                _logger.LogWarning(
                    "Account locked after {Attempts} failed attempts: {Email}",
                    user.FailedLoginAttempts, email);
            }

            _uow.Users.Update(user);
            await _uow.SaveAsync();

            _loginFailed.Add(1, new TagList { { "reason", "wrong_password" } });
            _logger.LogWarning(
                "Login failed — wrong password for {Email} (attempt {Attempts})",
                email, user.FailedLoginAttempts);

            return Result<AuthResponseDTO>.Fail("Invalid credentials");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        // RememberMe extends the JWT lifetime so the cookie can stay signed in
        // across browser restarts without re-authenticating.
        var token = _token.GenerateToken(user, dto.RememberMe);

        _loginSuccess.Add(1, new TagList { { "user.role", user.UserRole.ToString() } });
        span?.SetTag("user.id", user.Id);
        span?.SetTag("user.role", user.UserRole.ToString());

        _logger.LogInformation(
            "Login successful for {Email} (UserId {UserId}, Role {Role})",
            email, user.Id, user.UserRole);
        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            UserRole = user.UserRole.ToString()
        });
    }

    // =========================
    // GOOGLE LOGIN
    // =========================
    public async Task<Result<AuthResponseDTO>> GoogleLoginAsync(GoogleLoginDTO dto)
    {
        using var span = _activitySource.StartActivity("GoogleLogin");

        _logger.LogInformation("Google login attempt");

        GoogleJsonWebSignature.Payload payload;

        try
        {
            var googleClientId = _config["Google:ClientId"];

            // ClientId must be configured to prevent confused-deputy attacks
            // (tokens issued for another Google app would otherwise be accepted).
            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                _logger.LogError("Google:ClientId is not configured");
                return Result<AuthResponseDTO>.Fail("Google login is not configured");
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            span?.AddException(ex);

            _loginFailed.Add(1, new TagList { { "reason", "invalid_google_token" } });
            _logger.LogWarning("Google login failed — invalid token");

            return Result<AuthResponseDTO>.Fail("Invalid Google token");
        }

        // Only accept Google accounts with a verified email to prevent
        // account takeover via unverified email impersonation.
        if (!payload.EmailVerified)
        {
            _loginFailed.Add(1, new TagList { { "reason", "email_not_verified" } });
            _logger.LogWarning(
                "Google login failed — email not verified: {Email}", payload.Email);
            return Result<AuthResponseDTO>.Fail("Google account email is not verified");
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        span?.SetTag("user.email", email);

        var user = await _uow.Users.GetByEmailAsync(email);

        if (user == null)
        {
            // Check IsActive-equivalent: new Google users are created active.
            // First-time Google sign-in: create Customer + User in a single transaction
            // so a failed second SaveAsync does not leave an orphan Customer record.
            await _uow.BeginTransactionAsync();
            try
            {
                var customerName = !string.IsNullOrWhiteSpace(payload.Name)
                    ? payload.Name
                    : email.Split('@')[0];

                var customer = new Customer
                {
                    Name = customerName,
                    Email = email,
                    PhoneNumber = string.Empty,
                };
                await _uow.Customers.AddAsync(customer);
                await _uow.SaveAsync();

                user = new User
                {
                    Email = email,
                    GoogleId = payload.Subject,
                    UserRole = UserRoles.Client,
                    IsActive = true,
                    PasswordHash = string.Empty,
                    CustomerId = customer.Id,
                };

                await _uow.Users.AddAsync(user);
                await _uow.SaveAsync();
                await _uow.CommitAsync();

                _logger.LogInformation(
                    "New user created via Google login: {Email} (CustomerId {CustomerId})",
                    email, customer.Id);
            }
            catch (Exception ex)
            {
                await _uow.RollbackAsync();
                _logger.LogError(ex, "Google registration failed for {Email}", email);
                return Result<AuthResponseDTO>.Fail("Registration failed. Please try again.");
            }
        }
        else
        {
            // Check IsActive before linking or generating a token.
            if (!user.IsActive)
            {
                _loginFailed.Add(1, new TagList { { "reason", "account_inactive" } });
                _logger.LogWarning(
                    "Google login failed — account inactive: {Email}", email);
                return Result<AuthResponseDTO>.Fail("Account is inactive");
            }

            if (string.IsNullOrEmpty(user.GoogleId))
            {
                // Existing email/password account signing in with Google for the first
                // time — link the Google identity so future Google sign-ins resolve
                // to the same record.
                user.GoogleId = payload.Subject;
                _uow.Users.Update(user);
                await _uow.SaveAsync();

                _logger.LogInformation(
                    "Existing user {Email} linked to Google id", email);
            }
        }

        var token = _token.GenerateToken(user, dto.RememberMe);

        _googleLogins.Add(1);
        span?.SetTag("user.id", user.Id);

        _logger.LogInformation(
            "Google login successful for {Email} (UserId {UserId})",
            email, user.Id);

        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            UserRole = user.UserRole.ToString()
        });
    }

    // =========================
    // REGISTER CLIENT
    // =========================
    public async Task<Result<AuthResponseDTO>> RegisterAsync(RegisterDTO dto)
    {
        using var span = _activitySource.StartActivity("Register");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<AuthResponseDTO>.Fail("Name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return Result<AuthResponseDTO>.Fail("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            return Result<AuthResponseDTO>.Fail("Password is required.");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return Result<AuthResponseDTO>.Fail("Phone number is required.");

        var email = dto.Email.Trim().ToLowerInvariant();
        span?.SetTag("user.email", email);

        _logger.LogInformation("Client registration attempt for {Email}", email);

        // Enforce unique email — the Users table has no DB-level unique constraint
        // so we check explicitly to return a friendly error instead of a 500.
        var existing = await _uow.Users.GetByEmailAsync(email);
        if (existing != null)
        {
            _logger.LogWarning("Registration failed — email already in use: {Email}", email);
            return Result<AuthResponseDTO>.Fail("This email is already registered.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            // Create the customer profile first so we can link it to the user.
            var customer = new Customer
            {
                Name = dto.Name.Trim(),
                Email = email,
                PhoneNumber = dto.PhoneNumber.Trim(),
                DateOfBirth = dto.DateOfBirth,
            };
            await _uow.Customers.AddAsync(customer);
            await _uow.SaveAsync();

            // Create the user account with a hashed password and Client role.
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                UserRole = UserRoles.Client,
                IsActive = true,
                CustomerId = customer.Id,
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveAsync();
            await _uow.CommitAsync();

            var token = _token.GenerateToken(user);

            _loginSuccess.Add(1, new TagList { { "user.role", user.UserRole.ToString() } });
            _logger.LogInformation(
                "Client registered successfully: {Email} (UserId {UserId})",
                email, user.Id);

            return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
            {
                Token = token,
                Email = user.Email,
                UserRole = user.UserRole.ToString(),
            });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Registration failed for {Email}", email);
            return Result<AuthResponseDTO>.Fail("Registration failed. Please try again.");
        }
    }

    // =========================
    // UNLOCK USER
    // =========================
    public async Task<Result<bool>> UnlockUserAsync(int userId)
    {
        using var span = _activitySource.StartActivity("UnlockUser");
        span?.SetTag("user.id", userId);

        _logger.LogInformation("Unlocking user {UserId}", userId);

        var user = await _uow.Users.GetByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Unlock failed — user {UserId} not found", userId);
            return Result<bool>.Ok(false);
        }

        user.LockoutEnd = null;
        user.FailedLoginAttempts = 0;

        _uow.Users.Update(user);
        await _uow.SaveAsync();

        _accountsUnlocked.Add(1);

        _logger.LogInformation("User {UserId} unlocked successfully", userId);

        return Result<bool>.Ok(true);
    }
}
