using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuthService _authService;

    public AuthController(AppDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet("login")]
    public async Task<AuthResponseDTO?> LoginAsync([FromBody]LoginDTO dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
            return null;

        // Check if user is locked out
        if (user.LockoutEnd != null && user.LockoutEnd > DateTime.UtcNow)
        {
            TimeSpan lockOutEnd = ((DateTime)user.LockoutEnd - DateTime.UtcNow);
            throw new Exception($"User is locked. Try again in {lockOutEnd} minutes");
        }


        // Secret check
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddHours(2);
                user.FailedLoginAttempts = 0;
            }

            await _context.SaveChangesAsync();
            return null;
        }

        // Reset after successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        await _context.SaveChangesAsync();

        var token = _authService.GenerateToken(user);

        return new AuthResponseDTO
        {
            Token = token,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody]GoogleLoginDTO dto)
    {
        var result = await _authService.GoogleLoginAsync(dto.IdToken);

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("unlock/{userId}")]
    public async Task<IActionResult> UnlockUser(int userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound();

        user.LockoutEnd = null;
        user.FailedLoginAttempts = 0;

        await _context.SaveChangesAsync();

        return Ok("User unlocked");
    }
}
