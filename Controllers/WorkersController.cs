using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
        var workers = await _context.Workers.Include(w => w.ProvidedServices).ToListAsync();
        return Ok(workers);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _context.Workers
            .Where(w => w.Id == id)
            .Select(w => new WorkerDTO
            {
                Id = w.Id,
                Name = w.Name,
                PhoneNumber = w.PhoneNumber,
                Address = w.Address,
                DateOfBirth = w.DateOfBirth,
                WagePerHour = w.WagePerHour,
                Position = w.Position,
                ProvidedServices = w.ProvidedServices
                    .Select(s => new ServiceDTO
                    {
                        Name = s.Name,
                        Duration = s.Duration,
                        Price = s.Price
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        if (dto == null)
            return NotFound();

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var worker = await _context.Workers.FindAsync(id);
        if (worker == null)
            return NotFound();
        _context.Workers.Remove(worker);
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the deletion
        await _hubContext.Clients.All.SendAsync("WorkerChanged");
        return NoContent();
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update([FromBody] WorkerDTO worker, int id)
    {
        var existingWorker = await _context.Workers.FindAsync(id);
        if (existingWorker == null)
            return NotFound();
        var services = await _context.Services
            .Where(s => worker.ServicesId.Contains(s.Id))
            .ToListAsync();

        existingWorker.Name = worker.Name;
        existingWorker.PhoneNumber = worker.PhoneNumber;
        existingWorker.Address = worker.Address;
        existingWorker.DateOfBirth = worker.DateOfBirth;
        existingWorker.ProvidedServices = services;
        existingWorker.WagePerHour = worker.WagePerHour;
        existingWorker.Position = worker.Position;
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the update
        await _hubContext.Clients.All.SendAsync("WorkerChanged");
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
        var services = await _context.Services
            .Where(s => worker.ServicesId.Contains(s.Id))
            .ToListAsync();
        var newWorker = new Worker
        {
            Name = worker.Name,
            PhoneNumber = worker.PhoneNumber,
            Address = worker.Address,
            DateOfBirth = worker.DateOfBirth,
            ProvidedServices = services,
            WagePerHour = worker.WagePerHour,
            Position = worker.Position
        };
        await _context.Workers.AddAsync(newWorker);
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the new worker
        await _hubContext.Clients.All.SendAsync("WorkerChanged");
        return CreatedAtAction(nameof(GetById), new { id = newWorker.Id }, newWorker);
    }

    [HttpGet("{workerId : int}")]
    public async Task<IActionResult> GetServicesByWorker(int id) // Fix
    {
        var worker = await _context.Workers.Include(w => w.ProvidedServices).FirstOrDefaultAsync(w => w.Id == id);
        if (worker == null)
            return NotFound();
        return Ok(worker.ProvidedServices);
    }

    [HttpGet("{serviceName : string}")]
    public async Task<IActionResult> GetWorkersByService(string serviceName)
    {
        var list = await _context.Workers.Where(s => s.ProvidedServices.Any(p => p.Name == serviceName)).ToListAsync();
        if (!list.Any())
            return NotFound();
        return Ok(list);
    }
}
