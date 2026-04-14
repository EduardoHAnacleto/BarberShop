using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.WSIdentity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BarberShop.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly TokenService _token;

    public AuthService(IUnitOfWork uow, TokenService token)
    {
        _uow = uow;
        _token = token;
    }

    // =========================
    // LOGIN
    // =========================
    public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto)
    {
        var user = await _uow.Users.GetByEmailAsync(dto.Email);

        if (user == null || !user.IsActive)
            return Result<AuthResponseDTO>.Fail("Invalid credentials");

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return Result<AuthResponseDTO>.Fail("Account is locked");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);

            _uow.Users.Update(user);
            await _uow.SaveAsync();

            return Result<AuthResponseDTO>.Fail("Invalid credentials");
        }

        // Reset failed attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        _uow.Users.Update(user);
        await _uow.SaveAsync();

        var token = _token.GenerateToken(user);

        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            Role = user.Role.ToString()
        });
    }

    // =========================
    // GOOGLE LOGIN
    // =========================
    public async Task<Result<AuthResponseDTO>> GoogleLoginAsync(GoogleLoginDTO dto)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken);
        }
        catch
        {
            return Result<AuthResponseDTO>.Fail("Invalid Google token");
        }

        var user = await _uow.Users.GetByEmailAsync(payload.Email);

        if (user == null)
        {
            user = new User
            {
                Email = payload.Email,
                GoogleId = payload.Subject,
                Role = UserRoles.Client,
                IsActive = true,
                PasswordHash = string.Empty
            };

            await _uow.Users.AddAsync(user);
            await _uow.SaveAsync();
        }

        if (!user.IsActive)
            return Result<AuthResponseDTO>.Fail("Account is inactive");

        var token = _token.GenerateToken(user);

        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            Role = user.Role.ToString()
        });
    }

    // =========================
    // UNLOCK USER
    // =========================
    public async Task<Result<bool>> UnlockUserAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);

        if (user == null)
            return Result<bool>.Ok(false);

        user.LockoutEnd = null;
        user.FailedLoginAttempts = 0;

        _uow.Users.Update(user);
        await _uow.SaveAsync();

        return Result<bool>.Ok(true);
    }
}