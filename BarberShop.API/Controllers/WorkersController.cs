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

    public WorkersController(IWorkersService workerService)
    {
        _workerService = workerService;
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

        if (result.Data == null)
            return NotFound();

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