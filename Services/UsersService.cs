using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Services;

public class UsersService : BaseService, IUsersService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<UsersHub> _hub;

    public UsersService(
        IUnitOfWork uow,
        IMapper mapper,
        RedisService redis,
        IHubContext<UsersHub> hub) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<UserResponseDTO>> GetAllAsync()
    {
        var result = await GetCachedAsync(
            "users:all",
            async () => _mapper.Map<List<UserResponseDTO>>(await _uow.Users.GetAllAsync())
        );

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<UserResponseDTO?> GetByIdAsync(int id)
    {
        return await GetCachedAsync(
            $"users:{id}",
            async () =>
            {
                var user = await _uow.Users.GetByIdAsync(id);
                return user == null ? null : _mapper.Map<UserResponseDTO>(user);
            }
        );
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<UserResponseDTO>> Create(UserRequestDTO dto)
    {
        var exists = await _uow.Users.GetAllAsync(u => u.Email == dto.Email);

        if (exists.Any())
            return Result<UserResponseDTO>.Fail("Email already exists");

        var user = _mapper.Map<User>(dto);

        await _uow.Users.AddAsync(user);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("users", _hub, "UsersChanged");

        return Result<UserResponseDTO>.Ok(_mapper.Map<UserResponseDTO>(user));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<UserResponseDTO>> Update(int id, UserRequestDTO dto)
    {
        var user = await _uow.Users.GetByIdAsync(id);

        if (user == null)
            return Result<UserResponseDTO>.Ok(null);

        _mapper.Map(dto, user);

        _uow.Users.Update(user);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("users", _hub, "UsersChanged");

        return Result<UserResponseDTO>.Ok(_mapper.Map<UserResponseDTO>(user));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<UserResponseDTO>> Delete(int id)
    {
        var user = await _uow.Users.GetByIdAsync(id);

        if (user == null)
            return Result<UserResponseDTO>.Ok(null);

        _uow.Users.Delete(user);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("users", _hub, "UsersChanged");

        return Result<UserResponseDTO>.Ok(null);
    }
}