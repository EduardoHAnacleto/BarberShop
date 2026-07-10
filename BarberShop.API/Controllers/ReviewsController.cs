using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BarberShop.API.Controllers;

// Public endpoints (worker reviews + summary) power the star ratings shown in
// the booking flow; the mutating endpoints are scoped to the owning customer
// (create) or Admin (moderation), mirroring the CustomersController /me
// pattern used elsewhere in the API.
[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewsService _reviewsService;
    private readonly IUsersService _usersService;

    public ReviewsController(IReviewsService reviewsService, IUsersService usersService)
    {
        _reviewsService = reviewsService;
        _usersService = usersService;
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

    [AllowAnonymous]
    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
        => Ok(await _reviewsService.GetByWorkerAsync(workerId));

    [AllowAnonymous]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
        => Ok(await _reviewsService.GetSummaryAsync());

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return Ok(Array.Empty<ReviewResponseDTO>());

        return Ok(await _reviewsService.GetMineAsync(customerId.Value));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
        => Ok(await _reviewsService.GetAllAsync());

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReviewRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var customerId = await ResolveOwnCustomerIdAsync();
        if (customerId == null)
            return Forbid();

        var result = await _reviewsService.Create(customerId.Value, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetByWorker), new { workerId = result.Data!.WorkerId }, result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _reviewsService.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        return NoContent();
    }
}
