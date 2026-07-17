using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BarberShop.API.Controllers;

// Self-service join/leave for clients, full visibility for Admin — same
// /me-resolution pattern as CustomersController and ReviewsController.
[ApiController]
[Authorize]
[Route("api/waitlist")]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _waitlistService;
    private readonly IUsersService _usersService;

    public WaitlistController(IWaitlistService waitlistService, IUsersService usersService)
    {
        _waitlistService = waitlistService;
        _usersService = usersService;
    }

    private async Task<int?> ResolveOwnCustomerIdAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(sub, out var userId))
            return null;

        var user = await _usersService.GetByIdAsync(userId);
        return user?.CustomerId;
    }

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] WaitlistRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return Forbid();

        var result = await _waitlistService.Join(customerId.Value, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetMine), result.Data);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return Ok(Array.Empty<WaitlistResponseDTO>());

        return Ok(await _waitlistService.GetMineAsync(customerId.Value));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
        => Ok(await _waitlistService.GetAllAsync());

    // Admins may remove any entry (moderation); clients may only remove
    // their own — enforced in the service, not just by branching here.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        if (User.IsInRole("Admin"))
        {
            var adminResult = await _waitlistService.Delete(id);
            return adminResult.Success ? NoContent() : BadRequest(adminResult.Error);
        }

        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return Forbid();

        var result = await _waitlistService.Leave(customerId.Value, id);
        return result.Success ? NoContent() : BadRequest(result.Error);
    }
}
