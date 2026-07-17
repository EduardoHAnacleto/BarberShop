using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.API.Controllers;

[ApiController]
[Route("api/workers/{workerId:int}/schedule")]
public class WorkerScheduleController : ControllerBase
{
    private readonly IWorkerScheduleService _service;

    public WorkerScheduleController(IWorkerScheduleService service)
    {
        _service = service;
    }

    // Public — the booking flow's availability computation doesn't need this
    // directly (AvailabilityService reads the repository itself), but an
    // admin schedule-management UI and any future worker-facing view can.
    [HttpGet]
    public async Task<IActionResult> GetByWorker(int workerId)
        => Ok(await _service.GetByWorkerAsync(workerId));

    [Authorize(Roles = "Admin")]
    [HttpPut("{day}")]
    public async Task<IActionResult> Upsert(int workerId, DayOfWeek day, [FromBody] WorkerScheduleDTO dto)
    {
        var result = await _service.UpsertAsync(workerId, day, dto);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{day}")]
    public async Task<IActionResult> RemoveOverride(int workerId, DayOfWeek day)
    {
        var result = await _service.RemoveOverrideAsync(workerId, day);

        if (!result.Success)
            return BadRequest(result.Error);

        return result.Data ? NoContent() : NotFound();
    }
}
