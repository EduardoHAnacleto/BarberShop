using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IWorkerRepository _repository;
    private readonly IWorkerService _workerService;

    public WorkersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<WorkersHub> hubContext, IConfiguration configuration, IMapper mapper, IWorkerRepository repository,
        IWorkerService workerService)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _mapper = mapper;
        _repository = repository;
        _workerService = workerService;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var workers = await _repository.GetAllAsync(
            includes : w => w.ProvidedServices);
        var dtoList = _mapper.Map<List<WorkerDTO>>(workers);

        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var obj = await _repository.GetByIdAsync(id, w => w.ProvidedServices);
        if (obj == null)
            return NotFound();
        var dto = _mapper.Map<WorkerDTO>(obj);

        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var worker = await _repository.GetByIdAsync(id);
        if (worker == null)
            return NotFound();
        _repository.Delete(worker);
        await _context.SaveChangesAsync();
        // Invalidate cache for workers
        //await _redis.InvalidateByPrefixAsync("workers");
        // Notify clients about the deletion
        await _hubContext.Clients.All.SendAsync("WorkersChanged");
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update([FromBody] WorkerDTO worker, int id)
    {
        var existingWorker = await _repository.GetByIdAsync(id, x => x.ProvidedServices);
        if (existingWorker == null)
            return NotFound();
        _mapper.Map(worker, existingWorker);
        existingWorker.LastUpdatedAt = DateTime.UtcNow;
        _repository.Update(existingWorker);
        await _context.SaveChangesAsync(); 
        //await _redis.InvalidateByPrefixAsync("workers");
        await _hubContext.Clients.All.SendAsync("WorkersChanged");
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WorkerDTO worker)
    {
        if (string.IsNullOrWhiteSpace(worker.Name) || worker.Name.Length < 10)
        {
            return BadRequest();
        };
        if (worker.PhoneNumber == string.Empty || worker.PhoneNumber.Length < 7 || worker.PhoneNumber.Length>15)
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
        if (worker.ProvidedServices == null)
        {
            return BadRequest();
        }

        var newWorker = await _workerService.CreateFromDTO(worker);


        await _repository.AddAsync(newWorker);
        await _context.SaveChangesAsync();
        //await _redis.InvalidateByPrefixAsync("workers");

        await _hubContext.Clients.All.SendAsync("WorkersChanged");
        return CreatedAtAction(nameof(GetById), new { id = newWorker.Id }, worker);
    }

    [HttpGet("by-worker/{id:int}")]
    public async Task<IActionResult> GetServicesByWorker(int id) // Fix
    {
        var worker = await _repository.GetByIdAsync(id, p => p.ProvidedServices);
        if (worker == null)
            return NotFound();
        var dtoList = _mapper.Map<List<ServiceDTO>>(worker.ProvidedServices);
        return Ok(dtoList);
    }

    [HttpGet("by-service/{id:int}")]
    public async Task<IActionResult> GetWorkersByService(int id)
    {
        var list = await _repository.GetAllAsync(s => s.ProvidedServices.Any(p => p.Id == id));
        if (!list.Any())
            return NotFound();
        var dtoList = _mapper.Map<List<WorkerDTO>>(list);
        return Ok(dtoList);
    }
}
