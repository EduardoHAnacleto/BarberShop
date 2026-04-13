using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Services;

public class ServicesService : IServicesService
{
    private readonly IServiceRepository _repository;
    private readonly IMapper _mapper;
    private readonly AppDbContext _context;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;

    public ServicesService(
        IServiceRepository repository,
        IMapper mapper,
        AppDbContext context,
        RedisService redis,
        IHubContext<WorkersHub> hubContext)
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
    public async Task<List<ServiceDTO>> GetAllAsync()
    {
        var cacheKey = "services:all";

        var cached = await _redis.GetAsync<List<ServiceDTO>>(cacheKey);
        if (cached != null)
            return cached;

        var services = await _repository.GetAllAsync();

        var dtoList = _mapper.Map<List<ServiceDTO>>(services);

        await _redis.SetAsync(cacheKey, dtoList, TimeSpan.FromMinutes(10));

        return dtoList;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<ServiceDTO?> GetByIdAsync(int id)
    {
        var cacheKey = $"services:{id}";

        var cached = await _redis.GetAsync<ServiceDTO>(cacheKey);
        if (cached != null)
            return cached;

        var service = await _repository.GetByIdAsync(id);

        if (service == null)
            return null;

        var dto = _mapper.Map<ServiceDTO>(service);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return dto;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<ServiceDTO>> Create(ServiceDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 3)
            return Result<ServiceDTO>.Fail("Invalid Name");

        if (dto.Duration <= 0)
            return Result<ServiceDTO>.Fail("Invalid Duration");

        if (dto.Price <= 0)
            return Result<ServiceDTO>.Fail("Invalid Price");

        var entity = _mapper.Map<Service>(dto);

        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("services");
        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        var resultDto = _mapper.Map<ServiceDTO>(entity);

        return Result<ServiceDTO>.Ok(resultDto);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<ServiceDTO>> Update(int id, ServiceDTO dto)
    {
        var service = await _repository.GetByIdAsync(id);

        if (service == null)
            return Result<ServiceDTO>.Ok(null);

        _mapper.Map(dto, service);
        service.LastUpdatedAt = DateTime.UtcNow;

        _repository.Update(service);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("services");
        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        var resultDto = _mapper.Map<ServiceDTO>(service);

        return Result<ServiceDTO>.Ok(resultDto);
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<ServiceDTO>> Delete(int id)
    {
        var service = await _repository.GetByIdAsync(id);

        if (service == null)
            return Result<ServiceDTO>.Ok(null);

        _repository.Delete(service);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("services");
        await _hubContext.Clients.All.SendAsync("ServicesChanged");

        return Result<ServiceDTO>.Ok(null);
    }
}