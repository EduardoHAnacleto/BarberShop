using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using Google.Apis.Auth;
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

    public AuthService(
        IUnitOfWork uow,
        TokenService token,
        ILogger<AuthService> logger)
    {
        _uow = uow;
        _token = token;
        _logger = logger;
    }

    // =========================
    // LOGIN
    // =========================
    public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto)
    {
        using var span = _activitySource.StartActivity("Login");
        span?.SetTag("user.email", dto.Email);

        _logger.LogInformation("Login attempt for {Email}", dto.Email);

        var user = await _uow.Users.GetByEmailAsync(dto.Email);

        if (user == null || !user.IsActive)
        {
            _loginFailed.Add(1, new TagList { { "reason", "user_not_found_or_inactive" } });
            _logger.LogWarning("Login failed — user not found or inactive: {Email}", dto.Email);
            return Result<AuthResponseDTO>.Fail("Invalid credentials");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            _loginFailed.Add(1, new TagList { { "reason", "account_locked" } });
            _logger.LogWarning(
                "Login failed — account locked until {LockoutEnd}: {Email}",
                user.LockoutEnd, dto.Email);
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
                    user.FailedLoginAttempts, dto.Email);
            }

            _uow.Users.Update(user);
            await _uow.SaveAsync();

            _loginFailed.Add(1, new TagList { { "reason", "wrong_password" } });
            _logger.LogWarning(
                "Login failed — wrong password for {Email} (attempt {Attempts})",
                dto.Email, user.FailedLoginAttempts);

            return Result<AuthResponseDTO>.Fail("Invalid credentials");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        var token = _token.GenerateToken(user);

        _loginSuccess.Add(1, new TagList { { "user.role", user.UserRole.ToString() } });
        span?.SetTag("user.id", user.Id);
        span?.SetTag("user.role", user.UserRole.ToString());

        _logger.LogInformation(
            "Login successful for {Email} (UserId {UserId}, Role {Role})",
            dto.Email, user.Id, user.UserRole);
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
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken);
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            span?.AddException(ex);

            _loginFailed.Add(1, new TagList { { "reason", "invalid_google_token" } });
            _logger.LogWarning("Google login failed — invalid token");

            return Result<AuthResponseDTO>.Fail("Invalid Google token");
        }

        span?.SetTag("user.email", payload.Email);

        var user = await _uow.Users.GetByEmailAsync(payload.Email);

        if (user == null)
        {
            user = new User
            {
                Email = payload.Email,
                GoogleId = payload.Subject,
                UserRole = UserRoles.Client,
                IsActive = true,
                PasswordHash = string.Empty
            };

            await _uow.Users.AddAsync(user);
            await _uow.SaveAsync();

            _logger.LogInformation(
                "New user created via Google login: {Email}", payload.Email);
        }

        if (!user.IsActive)
        {
            _loginFailed.Add(1, new TagList { { "reason", "account_inactive" } });
            _logger.LogWarning(
                "Google login failed — account inactive: {Email}", payload.Email);
            return Result<AuthResponseDTO>.Fail("Account is inactive");
        }

        var token = _token.GenerateToken(user);

        _googleLogins.Add(1);
        span?.SetTag("user.id", user.Id);

        _logger.LogInformation(
            "Google login successful for {Email} (UserId {UserId})",
            payload.Email, user.Id);

        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            UserRole = user.UserRole.ToString()
        });
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