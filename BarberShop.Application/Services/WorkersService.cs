using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BarberShop.Application.Services;

public class WorkersService : BaseService, IWorkersService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.WorkersService");

    private static readonly Meter _meter =
        new("BarberShop.WorkersService");

    private static readonly Counter<long> _workersCreated =
        _meter.CreateCounter<long>(
            "barbershop.workers.created",
            description: "Total number of workers created");

    private static readonly Counter<long> _workersDeleted =
        _meter.CreateCounter<long>(
            "barbershop.workers.deleted",
            description: "Total number of workers deleted");

    private static readonly Histogram<double> _operationDuration =
        _meter.CreateHistogram<double>(
            "barbershop.workers.operation_duration",
            unit: "ms",
            description: "Duration of worker operations");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<WorkersHub> _hub;
    private readonly ILogger<WorkersService> _logger;

    public WorkersService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<WorkersHub> hub,
        ILogger<WorkersService> logger) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
        _logger = logger;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<WorkerDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllWorkers");

        _logger.LogInformation("Fetching all workers");

        var result = await GetCachedAsync(
            "workers:all",
            async () => _mapper.Map<List<WorkerDTO>>(
                await _uow.Workers.GetAllAsync(
                    null,
                    null,
                    w => w.ProvidedServices))
        );

        var count = result?.Count ?? 0;
        span?.SetTag("workers.count", count);
        _logger.LogInformation("Fetched {Count} workers", count);

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<WorkerDTO?> GetByIdAsync(int id)
    {
        using var span = _activitySource.StartActivity("GetWorkerById");
        span?.SetTag("worker.id", id);

        _logger.LogInformation("Fetching worker {WorkerId}", id);

        var result = await GetCachedAsync(
            $"workers:{id}",
            async () =>
            {
                var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);
                return worker == null ? null : _mapper.Map<WorkerDTO>(worker);
            }
        );

        if (result == null)
            _logger.LogWarning("Worker {WorkerId} not found", id);

        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<WorkerDTO>> Create(WorkerDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateWorker");
        span?.SetTag("worker.name", dto.Name);
        span?.SetTag("worker.wage", dto.WagePerHour);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating worker {WorkerName} with wage {WagePerHour}",
            dto.Name, dto.WagePerHour);

        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 10)
        {
            _logger.LogWarning(
                "Worker creation failed — invalid name: {WorkerName}", dto.Name);
            return Result<WorkerDTO>.Fail("Invalid Name");
        }

        if (dto.WagePerHour <= 0)
        {
            _logger.LogWarning(
                "Worker creation failed — invalid wage: {Wage}", dto.WagePerHour);
            return Result<WorkerDTO>.Fail("Invalid Wage");
        }

        var worker = _mapper.Map<Worker>(dto);

        foreach (var serviceId in dto.ServicesId)
        {
            var service = await _uow.Services.GetByIdAsync(serviceId);

            if (service != null)
                worker.ProvidedServices.Add(service);
            else
                _logger.LogWarning(
                    "ServiceId {ServiceId} not found — skipping", serviceId);
        }

        await _uow.Workers.AddAsync(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        stopwatch.Stop();

        span?.SetTag("worker.id", worker.Id);
        span?.SetTag("worker.services.count", worker.ProvidedServices.Count);
        _workersCreated.Add(1);
        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "create" } });

        _logger.LogInformation(
            "Worker {WorkerId} ({WorkerName}) created with {ServicesCount} services in {ElapsedMs}ms",
            worker.Id, worker.Name,
            worker.ProvidedServices.Count,
            stopwatch.Elapsed.TotalMilliseconds);

        return Result<WorkerDTO>.Ok(_mapper.Map<WorkerDTO>(worker));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<WorkerDTO>> Update(int id, WorkerDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateWorker");
        span?.SetTag("worker.id", id);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Updating worker {WorkerId}", id);

        var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
        {
            _logger.LogWarning("Worker {WorkerId} not found for update", id);
            return Result<WorkerDTO>.Ok(null);
        }

        _mapper.Map(dto, worker);
        worker.LastUpdatedAt = DateTime.UtcNow;

        _uow.Workers.Update(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        stopwatch.Stop();

        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "update" } });

        _logger.LogInformation(
            "Worker {WorkerId} updated in {ElapsedMs}ms",
            id, stopwatch.Elapsed.TotalMilliseconds);

        return Result<WorkerDTO>.Ok(_mapper.Map<WorkerDTO>(worker));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<WorkerDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteWorker");
        span?.SetTag("worker.id", id);

        _logger.LogInformation("Deleting worker {WorkerId}", id);

        var worker = await _uow.Workers.GetByIdAsync(id);

        if (worker == null)
        {
            _logger.LogWarning("Worker {WorkerId} not found for deletion", id);
            return Result<WorkerDTO>.Ok(null);
        }

        _uow.Workers.Delete(worker);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("workers", _hub, "WorkersChanged");

        _workersDeleted.Add(1);

        _logger.LogInformation("Worker {WorkerId} deleted successfully", id);

        return Result<WorkerDTO>.Ok(null);
    }

    // =========================
    // SERVICES BY WORKER
    // =========================
    public async Task<List<ServiceDTO>?> GetServicesByWorker(int id)
    {
        using var span = _activitySource.StartActivity("GetServicesByWorker");
        span?.SetTag("worker.id", id);

        _logger.LogInformation("Fetching services for worker {WorkerId}", id);

        var worker = await _uow.Workers.GetByIdAsync(id, w => w.ProvidedServices);

        if (worker == null)
        {
            _logger.LogWarning("Worker {WorkerId} not found", id);
            return null;
        }

        var services = _mapper.Map<List<ServiceDTO>>(worker.ProvidedServices);
        span?.SetTag("services.count", services.Count);

        return services;
    }

    // =========================
    // WORKERS BY SERVICE
    // =========================
    public async Task<List<WorkerDTO>> GetWorkersByService(int id)
    {
        using var span = _activitySource.StartActivity("GetWorkersByService");
        span?.SetTag("service.id", id);

        _logger.LogInformation("Fetching workers for service {ServiceId}", id);

        var workers = await _uow.Workers.GetAllAsync(
            w => w.ProvidedServices.Any(s => s.Id == id),
            null,
            w => w.ProvidedServices
        );

        span?.SetTag("workers.count", workers.Count);

        return _mapper.Map<List<WorkerDTO>>(workers);
    }
}