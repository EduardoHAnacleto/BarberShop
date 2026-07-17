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
    private readonly ISecurityStampService _stamps;
    private readonly IEmailService _email;

    public AuthService(
        IUnitOfWork uow,
        TokenService token,
        ILogger<AuthService> logger,
        IConfiguration config,
        ISecurityStampService stamps,
        IEmailService email)
    {
        _uow = uow;
        _token = token;
        _logger = logger;
        _config = config;
        _stamps = stamps;
        _email = email;
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

        // Tokens are long-lived and revoked via SecurityStamp rotation on
        // logout / credential change. RememberMe only affects how long the
        // frontend persists the cookie.
        var token = _token.GenerateToken(user);

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

        var token = _token.GenerateToken(user);

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

        try
        {
            // Create the customer profile and link it via the navigation property
            // (rather than a pre-fetched Id) so both rows insert in a single
            // SaveChangesAsync — one implicit transaction EF Core can retry as a
            // unit under EnableRetryOnFailure, instead of a manual BeginTransaction
            // that strategy does not support.
            var customer = new Customer
            {
                Name = dto.Name.Trim(),
                Email = email,
                PhoneNumber = dto.PhoneNumber.Trim(),
                DateOfBirth = dto.DateOfBirth,
            };
            await _uow.Customers.AddAsync(customer);

            // Create the user account with a hashed password and Client role.
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                UserRole = UserRoles.Client,
                IsActive = true,
                Customer = customer,
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveAsync();

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
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Registration failed for {Email}", email);
            return Result<AuthResponseDTO>.Fail("Registration failed. Please try again.");
        }
    }

    // =========================
    // LOGOUT
    // =========================
    // Rotates the user's SecurityStamp so every previously issued JWT stops
    // validating immediately. Tokens are long-lived by design; this is the
    // server-side "kill switch" the stamp claim exists for.
    public async Task<Result<bool>> LogoutAsync(int userId)
    {
        using var span = _activitySource.StartActivity("Logout");
        span?.SetTag("user.id", userId);

        var user = await _uow.Users.GetByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Logout failed — user {UserId} not found", userId);
            return Result<bool>.Ok(false);
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        // Drop the cached stamp so revocation takes effect on the next request.
        await _stamps.InvalidateCacheAsync(userId);

        _logger.LogInformation("User {UserId} logged out — tokens revoked", userId);

        return Result<bool>.Ok(true);
    }

    // =========================
    // CHANGE PASSWORD
    // =========================
    // Verifies the current password, stores a new BCrypt hash and rotates the
    // SecurityStamp so every other session (and the current token) is revoked
    // — the user re-authenticates with the new password.
    public async Task<Result<bool>> ChangePasswordAsync(
        int userId, string currentPassword, string newPassword)
    {
        using var span = _activitySource.StartActivity("ChangePassword");
        span?.SetTag("user.id", userId);

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return Result<bool>.Fail("New password must be at least 8 characters");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
            return Result<bool>.Fail("User not found");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            _logger.LogWarning(
                "Change password failed — wrong current password for UserId {UserId}", userId);
            return Result<bool>.Fail("Current password is incorrect");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        await _stamps.InvalidateCacheAsync(userId);

        _logger.LogInformation(
            "Password changed for UserId {UserId} — sessions revoked", userId);

        return Result<bool>.Ok(true);
    }

    // =========================
    // FORGOT PASSWORD
    // =========================
    // Never reveals whether the email is registered — the same generic
    // response goes out from the controller regardless of what happens here.
    // A found account gets a single-use token (1h) and a reset email.
    public async Task ForgotPasswordAsync(string email)
    {
        using var span = _activitySource.StartActivity("ForgotPassword");

        var normalized = email.Trim().ToLowerInvariant();
        var user = await _uow.Users.GetByEmailAsync(normalized);

        if (user == null || !user.IsActive)
        {
            _logger.LogInformation(
                "Forgot-password requested for {Email} — no active account, no email sent", normalized);
            return;
        }

        var token = GenerateResetToken();
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        var frontendBaseUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

        await _email.SendAsync(
            user.Email,
            user.Email,
            "Reset your BarberShop password",
            $"""
            <p>We received a request to reset your BarberShop password.</p>
            <p><a href="{resetLink}">Click here to choose a new password</a>. This link expires in 1 hour.</p>
            <p>If you did not request this, you can safely ignore this email.</p>
            """);

        _logger.LogInformation("Password reset token issued for UserId {UserId}", user.Id);
    }

    // =========================
    // RESET PASSWORD
    // =========================
    public async Task<Result<bool>> ResetPasswordAsync(string token, string newPassword)
    {
        using var span = _activitySource.StartActivity("ResetPassword");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return Result<bool>.Fail("New password must be at least 8 characters");

        if (string.IsNullOrWhiteSpace(token))
            return Result<bool>.Fail("Invalid or expired reset link");

        var users = await _uow.Users.GetAllAsync(u => u.PasswordResetToken == token);
        var user = users.SingleOrDefault();

        if (user == null || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Reset-password failed — invalid or expired token");
            return Result<bool>.Fail("Invalid or expired reset link");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        await _stamps.InvalidateCacheAsync(user.Id);

        _logger.LogInformation(
            "Password reset via token for UserId {UserId} — sessions revoked", user.Id);

        return Result<bool>.Ok(true);
    }

    // Cryptographically random, URL-safe token — not the JWT/BCrypt libraries
    // already in use, so a dedicated generator instead of overloading either.
    private static string GenerateResetToken()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
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
