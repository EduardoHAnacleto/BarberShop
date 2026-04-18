using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/working-hours")]
public class WorkingHoursController : ControllerBase
{
    private readonly IWorkingHoursService _service;

    public WorkingHoursController(IWorkingHoursService service)
    {
        _service = service;
    }

    // -- Schedule (standard business hours) --

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule()
        => Ok(await _service.GetScheduleAsync());

    [HttpGet("schedule/{day}")]
    public async Task<IActionResult> GetScheduleByDay(DayOfWeek day)
    {
        var result = await _service.GetScheduleByDayAsync(day);
        return result == null ? NotFound() : Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("schedule/{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] BusinessScheduleDTO dto)
    {
        var result = await _service.UpdateScheduleAsync(id, dto);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    // -- Closures --

    [HttpGet("closures")]
    public async Task<IActionResult> GetClosures()
        => Ok(await _service.GetClosuresAsync());

    [Authorize(Roles = "Admin")]
    [HttpPost("closures")]
    public async Task<IActionResult> AddClosure([FromBody] WorkingHours closure)
    {
        var result = await _service.AddClosureAsync(closure);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("closures/{id:int}")]
    public async Task<IActionResult> RemoveClosure(int id)
    {
        var result = await _service.RemoveClosureAsync(id);

        if (!result.Success)
            return BadRequest(result.Error);

        return result.Data ? NoContent() : NotFound();
    }

    [HttpGet("is-open")]
    public async Task<IActionResult> IsOpen([FromQuery] DateTime dateTime)
        => Ok(new { isOpen = await _service.IsOpenAsync(dateTime) });
}