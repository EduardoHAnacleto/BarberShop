using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<UsersHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

    public UsersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<UsersHub> hubContext, IConfiguration configuration, IMapper mapper)
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
        var list = await _context.Users.ToListAsync();
        var dtoList = _mapper.Map<List<UserResponseDTO>>(list);
        return Ok(dtoList);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();
        var dto = _mapper.Map<List<UserResponseDTO>>(user);
        return Ok(dto);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
     public async Task<IActionResult> Update(int id, [FromBody] UserRequestDTO updatedUser)
    {
        var user = await _context.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound();
        user.Email = updatedUser.Email;
        user.PasswordHash = updatedUser.PasswordHash;
        user.Role = updatedUser.Role;
        user.IsActive = updatedUser.IsActive;
        user.WorkerId = updatedUser.WorkerId;
        user.CustomerId = updatedUser.CustomerId;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserRequestDTO newUser)
    {
        var user = new User
        {
            Email = newUser.Email,
            PasswordHash = newUser.PasswordHash,
            Role = newUser.Role,
            IsActive = true,
            CustomerId = newUser.CustomerId,
            WorkerId = newUser.WorkerId
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        var dto = _mapper.Map<List<UserResponseDTO>>(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }
}
