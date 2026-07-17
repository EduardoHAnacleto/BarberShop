using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BarberShop.API.Controllers;

[ApiController]
[Authorize]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentsService _service;
    private readonly IAppointmentAccessService _access;

    public AppointmentsController(
        IAppointmentsService service,
        IAppointmentAccessService access)
    {
        _service = service;
        _access = access;
    }

    // Caller identity resolved from the JWT for resource-level authorization.
    private int CallerUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            return int.TryParse(sub, out var id) ? id : 0;
        }
    }

    private bool CallerIsAdmin => User.IsInRole("Admin");

    // Admin-only: the global appointment list would otherwise leak every
    // customer's bookings to any authenticated user.
    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all/paged")]
    public async Task<IActionResult> GetAllPaged([FromQuery] PaginationParams pagination)
    => Ok(await _service.GetAllAsync(pagination));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        // Only the owning client/worker (or an admin) may read the appointment.
        if (!await _access.CanMutateAsync(CallerUserId, CallerIsAdmin, new[] { id }))
            return Forbid();

        return Ok(result);
    }

    // Anonymous: the public /book flow lets a first-time visitor schedule an
    // appointment without creating an account first.
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.Create(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    // Books a weekly-recurring series in one request. Conflicting occurrences
    // are skipped individually rather than failing the whole series — the
    // response reports which dates were actually created vs. skipped.
    // Anonymous for the same reason as Create above.
    [AllowAnonymous]
    [HttpPost("recurring")]
    public async Task<IActionResult> CreateRecurring([FromBody] RecurringAppointmentRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.CreateRecurring(dto);

        if (!result.Success)
            return BadRequest(result.Error);

        return Ok(result.Data);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!await _access.CanMutateAsync(CallerUserId, CallerIsAdmin, new[] { id }))
            return Forbid();

        var result = await _service.Update(id, dto);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return Ok(result.Data);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _access.CanMutateAsync(CallerUserId, CallerIsAdmin, new[] { id }))
            return Forbid();

        var result = await _service.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        return NoContent();
    }

    // Status transition for the worker portal: a worker may start, complete or
    // mark as no-show an appointment assigned to them (admins, any).
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDTO dto)
    {
        if (!await _access.CanMutateAsync(CallerUserId, CallerIsAdmin, new[] { id }))
            return Forbid();

        var result = await _service.ChangeStatus(id, dto.Status);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return Ok(result.Data);
    }

    // Batch cancel — used by the client portal's "Cancel" button. Every id in
    // the batch must belong to the caller (or the caller must be an admin).
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelAppointmentsDTO dto)
    {
        if (dto.IdList.Count == 0)
            return BadRequest("No appointments specified");

        if (!await _access.CanMutateAsync(CallerUserId, CallerIsAdmin, dto.IdList))
            return Forbid();

        var result = await _service.CancelAppointments(dto.IdList);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    // Batch delay — admins reschedule appointments forward by a time span.
    [Authorize(Roles = "Admin")]
    [HttpPost("delay")]
    public async Task<IActionResult> Delay([FromBody] DelayAppointmentsDTO dto)
    {
        if (dto.IdList.Count == 0)
            return BadRequest("No appointments specified");

        var result = await _service.DelayAppointments(dto.IdList, dto.TimeSpan);
        return result.Success ? Ok(result.Data) : BadRequest(result.Error);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("range")]
    public async Task<IActionResult> GetByRange(DateTime dateStart, DateTime dateEnd)
        => Ok(await _service.GetByDateRange(dateStart, dateEnd));

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        // Workers see only their own calendar; admins see anyone's.
        if (!await _access.CanViewWorkerAsync(CallerUserId, CallerIsAdmin, workerId))
            return Forbid();

        return Ok(await _service.GetByWorker(workerId));
    }

    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        // Clients see only their own appointments; admins see anyone's.
        if (!await _access.CanViewCustomerAsync(CallerUserId, CallerIsAdmin, customerId))
            return Forbid();

        return Ok(await _service.GetByCustomer(customerId));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
        => Ok(await _service.GetByService(serviceId));

    [Authorize(Roles = "Admin")]
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
        => Ok(await _service.GetByStatus(status));
}
