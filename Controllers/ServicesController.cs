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
public class ServicesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;

    public ServicesController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<WorkersHub> hubContext, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var services = await _context.Services.ToListAsync();
        return Ok(services);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        //var cacheKey = $"services:id:{id}";
        var service = await _context.Services.FindAsync(id);
        if (service == null)
            return NotFound();
        var dto = new ServiceDTO
        {
            Name = service.Name,
            Duration = service.Duration,
            Price = service.Price,
            Description = service.Description
        };

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
        await _redis.InvalidateByPrefixAsync("services");
        // Notify clients about the deletion
        await _hubContext.Clients.All.SendAsync("ServiceDeleted", id);
        return NoContent();
    }

    [HttpPatch("{id:int}")]
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
        await _redis.InvalidateByPrefixAsync("services");
        // Notify clients about the update
        await _hubContext.Clients.All.SendAsync("ServiceUpdated", service);
        return Ok(service);
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

        await _redis.InvalidateByPrefixAsync("services");
        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        return CreatedAtAction(nameof(GetById), new { id = obj.Id }, obj);
    }


}
