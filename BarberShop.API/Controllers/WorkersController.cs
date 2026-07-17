using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.API.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly IWorkersService _workerService;
    private readonly IAvailabilityService _availabilityService;

    public WorkersController(
        IWorkersService workerService,
        IAvailabilityService availabilityService)
    {
        _workerService = workerService;
        _availabilityService = availabilityService;
    }

    // Bookable "HH:mm" start times for a worker on a day for a given service.
    // Computed server-side (schedule, break, closures, existing bookings,
    // service duration fit, same-day lead time) so the booking UI no longer
    // downloads other customers' appointments to reason about free slots.
    [AllowAnonymous]
    [HttpGet("{id:int}/availability")]
    public async Task<IActionResult> GetAvailability(
        int id, [FromQuery] DateOnly date, [FromQuery] int serviceId)
    {
        var result = await _availabilityService.GetAvailabilityAsync(id, date, serviceId);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    [AllowAnonymous]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _workerService.GetAllAsync();
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("all/paged")]
    public async Task<IActionResult> GetAllPaged([FromQuery] PaginationParams pagination)
    => Ok(await _workerService.GetAllAsync(pagination));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _workerService.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WorkerDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _workerService.Create(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] WorkerDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _workerService.Update(id, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _workerService.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("by-worker/{id:int}")]
    public async Task<IActionResult> GetServicesByWorker(int id)
    {
        var result = await _workerService.GetServicesByWorker(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("by-service/{id:int}")]
    public async Task<IActionResult> GetWorkersByService(int id)
    {
        var result = await _workerService.GetWorkersByService(id);

        if (result == null || !result.Any())
            return NotFound();

        return Ok(result);
    }
}