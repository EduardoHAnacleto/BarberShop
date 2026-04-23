using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.API.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly IWorkersService _workerService;

    public WorkersController(IWorkersService workerService)
    {
        _workerService = workerService;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _workerService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _workerService.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _workerService.Delete(id);

        if (!result.Success)
            return BadRequest(result.Error);

        if (result.Data == null)
            return NotFound();

        return NoContent();
    }

    [HttpGet("by-worker/{id:int}")]
    public async Task<IActionResult> GetServicesByWorker(int id)
    {
        var result = await _workerService.GetServicesByWorker(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("by-service/{id:int}")]
    public async Task<IActionResult> GetWorkersByService(int id)
    {
        var result = await _workerService.GetWorkersByService(id);

        if (result == null || !result.Any())
            return NotFound();

        return Ok(result);
    }
}