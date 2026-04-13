using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Services;

public class UsersService : IUsersService
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;
    private readonly AppDbContext _context;
    private readonly RedisService _redis;
    private readonly IHubContext<UsersHub> _hubContext;

    public UsersService(
        IUserRepository repository,
        IMapper mapper,
        AppDbContext context,
        RedisService redis,
        IHubContext<UsersHub> hubContext)
    {
        _repository = repository;
        _mapper = mapper;
        _context = context;
        _redis = redis;
        _hubContext = hubContext;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<UserResponseDTO>> GetAllAsync()
    {
        var cacheKey = "users:all";

        var cached = await _redis.GetAsync<List<UserResponseDTO>>(cacheKey);
        if (cached != null)
            return cached;

        var users = await _repository.GetAllAsync();

        var dtoList = _mapper.Map<List<UserResponseDTO>>(users);

        await _redis.SetAsync(cacheKey, dtoList, TimeSpan.FromMinutes(10));

        return dtoList;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<UserResponseDTO?> GetByIdAsync(int id)
    {
        var cacheKey = $"users:{id}";

        var cached = await _redis.GetAsync<UserResponseDTO>(cacheKey);
        if (cached != null)
            return cached;

        var user = await _repository.GetByIdAsync(id);

        if (user == null)
            return null;

        var dto = _mapper.Map<UserResponseDTO>(user);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return dto;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<UserResponseDTO>> Create(UserRequestDTO dto)
    {
        var exists = await _repository.GetAllAsync(u => u.Email == dto.Email);

        if (exists.Any())
            return Result<UserResponseDTO>.Fail("Email already exists");

        var user = _mapper.Map<User>(dto);

        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("users");
        await _hubContext.Clients.All.SendAsync("UsersChanged");

        var resultDto = _mapper.Map<UserResponseDTO>(user);

        return Result<UserResponseDTO>.Ok(resultDto);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<UserResponseDTO>> Update(int id, UserRequestDTO dto)
    {
        var user = await _repository.GetByIdAsync(id);

        if (user == null)
            return Result<UserResponseDTO>.Ok(null);

        _mapper.Map(dto, user);

        _repository.Update(user);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("users");
        await _hubContext.Clients.All.SendAsync("UsersChanged");

        var resultDto = _mapper.Map<UserResponseDTO>(user);

        return Result<UserResponseDTO>.Ok(resultDto);
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<UserResponseDTO>> Delete(int id)
    {
        var user = await _repository.GetByIdAsync(id);

        if (user == null)
            return Result<UserResponseDTO>.Ok(null);

        _repository.Delete(user);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("users");
        await _hubContext.Clients.All.SendAsync("UsersChanged");

        return Result<UserResponseDTO>.Ok(null);
    }
}
