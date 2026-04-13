using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace BarberShop.Services;

public class WorkerService : IWorkersService
{
    private readonly IWorkerRepository _repository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IMapper _mapper;
    private readonly AppDbContext _context;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;

    public WorkerService(
        IWorkerRepository workerRepository,
        IServiceRepository serviceRepository,
        IMapper mapper,
        AppDbContext context,
        RedisService redis,
        IHubContext<WorkersHub> hubContext)
    {
        _repository = workerRepository;
        _serviceRepository = serviceRepository;
        _mapper = mapper;
        _context = context;
        _redis = redis;
        _hubContext = hubContext;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<WorkerDTO>> GetAllAsync()
    {
        var cacheKey = "workers:all";

        var cached = await _redis.GetAsync<List<WorkerDTO>>(cacheKey);
        if (cached != null)
            return cached;

        var workers = await _repository.GetAllAsync(
            includes: w => w.ProvidedServices);

        var dtoList = _mapper.Map<List<WorkerDTO>>(workers);

        await _redis.SetAsync(cacheKey, dtoList, TimeSpan.FromMinutes(10));

        return dtoList;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<WorkerDTO?> GetByIdAsync(int id)
    {
        var cacheKey = $"workers:{id}";

        var cached = await _redis.GetAsync<WorkerDTO>(cacheKey);
        if (cached != null)
            return cached;

        var worker = await _repository.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
            return null;

        var dto = _mapper.Map<WorkerDTO>(worker);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return dto;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<WorkerDTO>> Create(WorkerDTO dto)
    {
        if (dto.Name.Length < 10)
            return Result<WorkerDTO>.Fail("Invalid Name");

        if (dto.WagePerHour <= 0)
            return Result<WorkerDTO>.Fail("Invalid Wage");

        var worker = _mapper.Map<Worker>(dto);

        foreach (var serviceId in dto.ServicesId)
        {
            var service = await _serviceRepository.GetByIdAsync(serviceId);
            if (service != null)
                worker.ProvidedServices.Add(service);
        }

        await _repository.AddAsync(worker);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("workers");
        await _hubContext.Clients.All.SendAsync("WorkersChanged");

        var resultDto = _mapper.Map<WorkerDTO>(worker);

        return Result<WorkerDTO>.Ok(resultDto);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<WorkerDTO>> Update(int id, WorkerDTO dto)
    {
        var worker = await _repository.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
            return Result<WorkerDTO>.Ok(null);

        _mapper.Map(dto, worker);
        worker.LastUpdatedAt = DateTime.UtcNow;

        _repository.Update(worker);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("workers");
        await _hubContext.Clients.All.SendAsync("WorkersChanged");

        var resultDto = _mapper.Map<WorkerDTO>(worker);

        return Result<WorkerDTO>.Ok(resultDto);
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<WorkerDTO>> Delete(int id)
    {
        var worker = await _repository.GetByIdAsync(id);

        if (worker == null)
            return Result<WorkerDTO>.Ok(null);

        _repository.Delete(worker);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("workers");
        await _hubContext.Clients.All.SendAsync("WorkersChanged");

        return Result<WorkerDTO>.Ok(null);
    }

    // =========================
    // SERVICES BY WORKER
    // =========================
    public async Task<List<ServiceDTO>?> GetServicesByWorker(int id)
    {
        var worker = await _repository.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
            return null;

        return _mapper.Map<List<ServiceDTO>>(worker.ProvidedServices);
    }

    // =========================
    // WORKERS BY SERVICE
    // =========================
    public async Task<List<WorkerDTO>> GetWorkersByService(int id)
    {
        var workers = await _repository.GetAllAsync(
            filter: w => w.ProvidedServices.Any(s => s.Id == id),
            includes: w => w.ProvidedServices
        );

        return _mapper.Map<List<WorkerDTO>>(workers);
    }

}