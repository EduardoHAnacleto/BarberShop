using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BarberShop.API.Controllers;

// Login/register/google are IP rate-limited to blunt brute-force attempts and
// the lockout-DoS. Logout/unlock are authenticated and left unthrottled.
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.RegisterAsync(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return Ok(result.Data);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return Ok(result.Data);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.GoogleLoginAsync(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return Ok(result.Data);
    }

    // Revokes every JWT previously issued for the calling user by rotating
    // their SecurityStamp. The frontend calls this before clearing the cookie.
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var result = await _authService.LogoutAsync(userId);

        if (!result.Success)
            return BadRequest(result.Error);

        if (!result.Data)
            return NotFound();

        return Ok("Logged out");
    }

    // Always returns the same generic message, whether or not the email is
    // registered, so the endpoint cannot be used to enumerate accounts.
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await _authService.ForgotPasswordAsync(dto.Email);

        return Ok("If that email is registered, a reset link has been sent.");
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);

        return result.Success ? Ok("Password reset successfully. Please sign in.") : BadRequest(result.Error);
    }

    // Authenticated self-service password change. Requires the current
    // password; on success every existing token (including this one) is
    // revoked and the client must sign in again.
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var result = await _authService.ChangePasswordAsync(
            userId, dto.CurrentPassword, dto.NewPassword);

        return result.Success ? Ok("Password changed") : BadRequest(result.Error);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("unlock/{userId}")]
    public async Task<IActionResult> UnlockUser(int userId)
    {
        var result = await _authService.UnlockUserAsync(userId);

        if (!result.Success)
            return BadRequest(result.Error);

        if (!result.Data)
            return NotFound();

        return Ok("User unlocked");
    }
}