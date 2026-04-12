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
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IServiceRepository _repository;

    public ServicesController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<WorkersHub> hubContext, IConfiguration configuration, IMapper mapper, IServiceRepository repository)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _mapper = mapper;
        _repository = repository;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var services = await _repository.GetAllAsync();
        var dtoList = _mapper.Map<List<ServiceDTO>>(services);
        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        //var cacheKey = $"services:id:{id}";
        var service = await _repository.GetByIdAsync(id);
        if (service == null)
            return NotFound();
        var dto = _mapper.Map<ServiceDTO>(service);

        //await _redis.SetAsync(cacheKey, service, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _repository.GetByIdAsync(id);
        if (service == null)
            return NotFound();
        _repository.Delete(service);
        await _context.SaveChangesAsync();
        // Invalidate cache for services
        //await _redis.InvalidateByPrefixAsync("services");
        // Notify clients about the deletion
        await _hubContext.Clients.All.SendAsync("ServicesChanged");
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceDTO updatedService)
    {
        var service = await _repository.GetByIdAsync(id);

        if (service == null)
            return NotFound();

        _mapper.Map(updatedService, service);
        service.LastUpdatedAt = DateTime.UtcNow;
        _repository.Update(service);
        await _context.SaveChangesAsync();

        //await _redis.InvalidateByPrefixAsync("services");

        await _hubContext.Clients.All.SendAsync("ServicesChanged");
        return Ok(_mapper.Map<ServiceDTO>(service));
    }

    [HttpPost]
    public async Task<IActionResult> Create ([FromForm] ServiceDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 3)
            return BadRequest("Invalid Name");

        if (dto.Duration <= 0)
            return BadRequest("Invalid Duration");

        if (dto.Price <= 0)
            return BadRequest("Invalid Price");

        var entity = _mapper.Map<Service>(dto);

        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, _mapper.Map<ServiceDTO>(entity));
    }


}
