using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
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
    private readonly IUserRepository _repository;

    public UsersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<UsersHub> hubContext, IConfiguration configuration, IMapper mapper, IUserRepository repository)
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
        var list = await _repository.GetAllAsync();
        var dtoList = _mapper.Map<List<UserResponseDTO>>(list);
        return Ok(dtoList);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
            return NotFound();
        var dto = _mapper.Map<UserResponseDTO>(user);
        return Ok(dto);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
            return NotFound();
        _repository.Delete(user);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
     public async Task<IActionResult> Update(int id, [FromBody] UserRequestDTO updatedUser)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
            return NotFound();

        _repository.Update(user);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserRequestDTO newUser)
    {
        var user = _mapper.Map<User>(newUser);

        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("UsersChanged");
        var dto = _mapper.Map<List<UserResponseDTO>>(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }
}
