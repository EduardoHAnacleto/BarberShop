using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BarberShop.API.Controllers;

// Management endpoints require Admin; the /me endpoints let a client read and
// update their own linked customer record (needed by the client portal, which
// otherwise could not load or edit its own profile).
[ApiController]
[Authorize]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomersService _customersService;
    private readonly IUsersService _usersService;
    private readonly ILoyaltyService _loyaltyService;

    public CustomersController(
        ICustomersService customersService,
        IUsersService usersService,
        ILoyaltyService loyaltyService)
    {
        _customersService = customersService;
        _usersService = usersService;
        _loyaltyService = loyaltyService;
    }

    // Resolves the caller's linked customerId from the JWT subject, or null.
    private async Task<int?> ResolveOwnCustomerIdAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!int.TryParse(sub, out var userId))
            return null;

        var user = await _usersService.GetByIdAsync(userId);
        return user?.CustomerId;
    }

    // Own customer record — used by the client portal profile card.
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return NotFound();

        var result = await _customersService.GetByIdAsync(customerId.Value);
        return result == null ? NotFound() : Ok(result);
    }

    // Own loyalty progress — completed visit count and distance to the next
    // configurable reward. Used by the client portal profile card.
    [HttpGet("me/loyalty")]
    public async Task<IActionResult> GetMyLoyalty()
    {
        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return NotFound();

        return Ok(await _loyaltyService.GetStatusAsync(customerId.Value));
    }

    // Update own customer record — used by the client portal profile editor.
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] CustomerDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return NotFound();

        var result = await _customersService.Update(customerId.Value, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return result.Data == null ? NotFound() : Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _customersService.GetAllAsync();
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all/paged")]
    public async Task<IActionResult> GetAllPaged([FromQuery] PaginationParams pagination)
    => Ok(await _customersService.GetAllAsync(pagination));


    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customersService.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    // Anonymous: the public /book flow creates a customer record inline for
    // first-time visitors who don't have an account yet. Also used by the
    // admin "New customer" form for authenticated admins.
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _customersService.Create(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _customersService.Update(id, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return Ok(result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customersService.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        return NoContent();
    }
}