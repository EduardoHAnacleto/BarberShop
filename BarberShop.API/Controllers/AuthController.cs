using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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