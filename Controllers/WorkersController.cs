using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;

    public WorkersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<WorkersHub> hubContext, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var workers = _context.Workers.ToList();
        return Ok(workers);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var worker = _context.Workers.Find(id);
        if (worker == null)
            return NotFound();
        var dto = new WorkerDTO
        {
            Name = worker.Name,
            PhoneNumber = worker.PhoneNumber,
            Address = worker.Address,
            DateOfBirth = worker.DateOfBirth,
            ProvidedServices = worker.ProvidedServices,
            WagePerHour = worker.WagePerHour,
            Position = worker.Position
        };

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var worker = _context.Workers.Find(id);
        if (worker == null)
            return NotFound();
        _context.Workers.Remove(worker);
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the deletion
        await _hubContext.Clients.All.SendAsync("WorkerDeleted", id);
        return NoContent();
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update([FromBody] WorkerDTO worker, int id)
    {
        var existingWorker = _context.Workers.Find(id);
        if (existingWorker == null)
            return NotFound();
        existingWorker.Name = worker.Name;
        existingWorker.PhoneNumber = worker.PhoneNumber;
        existingWorker.Address = worker.Address;
        existingWorker.DateOfBirth = worker.DateOfBirth;
        existingWorker.ProvidedServices = worker.ProvidedServices;
        existingWorker.WagePerHour = worker.WagePerHour;
        existingWorker.Position = worker.Position;
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the update
        await _hubContext.Clients.All.SendAsync("WorkerUpdated", id);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WorkerDTO worker)
    {
        if (string.IsNullOrWhiteSpace(worker.Name) || worker.Name.Length < 10)
        {
            return BadRequest();
        };
        if (worker.PhoneNumber == string.Empty || worker.PhoneNumber.Length < 7)
        {
            return BadRequest();
        };
        if (string.IsNullOrWhiteSpace(worker.Address) || worker.Address.Length < 10)
        {
            return BadRequest();
        };
        if (worker.DateOfBirth == default || worker.DateOfBirth > DateTime.Now.AddYears(-18))
        {
            return BadRequest();
        }
        if (worker.WagePerHour <= 0)
        {
            return BadRequest();
        }
        var newWorker = new Worker
        {
            Name = worker.Name,
            PhoneNumber = worker.PhoneNumber,
            Address = worker.Address,
            DateOfBirth = worker.DateOfBirth,
            ProvidedServices = worker.ProvidedServices,
            WagePerHour = worker.WagePerHour,
            Position = worker.Position
        };
        _context.Workers.Add(newWorker);
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the new worker
        await _hubContext.Clients.All.SendAsync("WorkerCreated", newWorker.Id);
        return CreatedAtAction(nameof(GetById), new { id = newWorker.Id }, newWorker);
    }
}
