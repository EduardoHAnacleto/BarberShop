using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentsService _service;

    public AppointmentsController(IAppointmentsService service)
    {
        _service = service;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

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

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
        var result = await _service.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return NoContent();
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetByRange(DateTime dateStart, DateTime dateEnd)
        => Ok(await _service.GetByDateRange(dateStart, dateEnd));

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
        => Ok(await _service.GetByWorker(workerId));

    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
        => Ok(await _service.GetByCustomer(customerId));

    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
        => Ok(await _service.GetByService(serviceId));

    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
        => Ok(await _service.GetByStatus(status));
}