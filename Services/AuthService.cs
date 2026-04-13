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

public class AuthService : BaseService, IAuthService
{
    private readonly IUserRepository _repo;
    private readonly TokenService _token;

    public AuthService(
        IUserRepository repo,
        TokenService token,
        AppDbContext context) : base(context)
    {
        _repo = repo;
        _token = token;
    }

    public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto)
    {
        var user = await _repo.GetByEmailAsync(dto.Email);

        if (user == null)
            return Result<AuthResponseDTO>.Fail("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Result<AuthResponseDTO>.Fail("Invalid credentials");

        user.AddDomainEvent(new UserLoggedInEvent(user.Id));

        await SaveAsync();

        var token = _token.GenerateToken(user);

        return Result<AuthResponseDTO>.Ok(new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            Role = user.Role.ToString()
        });
    }
}