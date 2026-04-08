using AutoMapper;
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
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

    public ServicesController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<WorkersHub> hubContext, IConfiguration configuration, IMapper mapper)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _mapper = mapper;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var services = await _context.Services.ToListAsync();
        var dtoList = _mapper.Map<List<ServiceDTO>>(services);
        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        //var cacheKey = $"services:id:{id}";
        var service = await _context.Services.FindAsync(id);
        if (service == null)
            return NotFound();
        var dto = _mapper.Map<ServiceDTO>(service);

        //await _redis.SetAsync(cacheKey, service, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = _context.Services.Find(id);
        if (service == null)
            return NotFound();
        _context.Services.Remove(service);
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
        var service = _context.Services.Find(id);

        if (service == null)
            return NotFound();
        service.Name = updatedService.Name;
        service.Duration = updatedService.Duration;
        service.Price = updatedService.Price;
        service.Description = updatedService.Description;

        await _context.SaveChangesAsync();
        // Invalidate cache for services
        //await _redis.InvalidateByPrefixAsync("services");
        // Notify clients about the update
        await _hubContext.Clients.All.SendAsync("ServicesChanged");
        return Ok(updatedService);
    }

    [HttpPost]
    public async Task<IActionResult> Create ([FromForm] ServiceDTO dto)
    {
        if (dto.Name == string.Empty || dto.Name.Length < 3)
        {
            return BadRequest();
        }
        if (dto.Duration <= 0)
        {
            return BadRequest();
        }
        if (dto.Price <= 0)
        {
            return BadRequest();
        }

        var obj = new Service
        {
            Name = dto.Name,
            Price = dto.Price,
            Description = dto.Description,
            Duration = dto.Duration
        };

        _context.Services.Add(obj);
        await _context.SaveChangesAsync();

        //await _redis.InvalidateByPrefixAsync("services");
        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        return CreatedAtAction(nameof(GetById), new { id = obj.Id }, dto);
    }


}
