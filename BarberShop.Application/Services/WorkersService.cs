using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Application.Services;

public class WorkersService : BaseService, IWorkersService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<WorkersHub> _hub;

    public WorkersService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<WorkersHub> hub) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<WorkerDTO>> GetAllAsync()
    {
        var result = await GetCachedAsync(
            "workers:all",
            async () => _mapper.Map<List<WorkerDTO>>(
                await _uow.Workers.GetAllAsync(
                    null,
                    null,
                    w => w.ProvidedServices))
        );

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<WorkerDTO?> GetByIdAsync(int id)
    {
        return await GetCachedAsync(
            $"workers:{id}",
            async () =>
            {
                var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);
                return worker == null ? null : _mapper.Map<WorkerDTO>(worker);
            }
        );
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<WorkerDTO>> Create(WorkerDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 10)
            return Result<WorkerDTO>.Fail("Invalid Name");

        if (dto.WagePerHour <= 0)
            return Result<WorkerDTO>.Fail("Invalid Wage");

        var worker = _mapper.Map<Worker>(dto);

        foreach (var serviceId in dto.ServicesId)
        {
            var service = await _uow.Services.GetByIdAsync(serviceId);
            if (service != null)
                worker.ProvidedServices.Add(service);
        }

        await _uow.Workers.AddAsync(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        return Result<WorkerDTO>.Ok(_mapper.Map<WorkerDTO>(worker));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<WorkerDTO>> Update(int id, WorkerDTO dto)
    {
        var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
            return Result<WorkerDTO>.Ok(null);

        _mapper.Map(dto, worker);
        worker.LastUpdatedAt = DateTime.UtcNow;

        _uow.Workers.Update(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        return Result<WorkerDTO>.Ok(_mapper.Map<WorkerDTO>(worker));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<WorkerDTO>> Delete(int id)
    {
        var worker = await _uow.Workers.GetByIdAsync(id);

        if (worker == null)
            return Result<WorkerDTO>.Ok(null);

        _uow.Workers.Delete(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        return Result<WorkerDTO>.Ok(null);
    }

    // =========================
    // SERVICES BY WORKER
    // =========================
    public async Task<List<ServiceDTO>?> GetServicesByWorker(int id)
    {
        var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
            return null;

        return _mapper.Map<List<ServiceDTO>>(worker.ProvidedServices);
    }

    // =========================
    // WORKERS BY SERVICE
    // =========================
    public async Task<List<WorkerDTO>> GetWorkersByService(int id)
    {
        var workers = await _uow.Workers.GetAllAsync(
            w => w.ProvidedServices.Any(s => s.Id == id),
            null,
            w => w.ProvidedServices
        );

        return _mapper.Map<List<WorkerDTO>>(workers);
    }
}