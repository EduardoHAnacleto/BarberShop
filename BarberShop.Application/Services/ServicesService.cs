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

public class ServicesService : BaseService, IServicesService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.ServicesService");

    private static readonly Meter _meter =
        new("BarberShop.ServicesService");

    private static readonly Counter<long> _servicesCreated =
        _meter.CreateCounter<long>(
            "barbershop.services.created",
            description: "Total number of services created");

    private static readonly Counter<long> _servicesDeleted =
        _meter.CreateCounter<long>(
            "barbershop.services.deleted",
            description: "Total number of services deleted");

    private static readonly Histogram<double> _operationDuration =
        _meter.CreateHistogram<double>(
            "barbershop.services.operation_duration",
            unit: "ms",
            description: "Duration of service operations");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<ServicesHub> _hub;
    private readonly ILogger<ServicesService> _logger;

    public ServicesService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<ServicesHub> hub,
        ILogger<ServicesService> logger) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
        _logger = logger;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<ServiceDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllServices");

        _logger.LogInformation("Fetching all services");

        var result = await GetCachedAsync(
            "services:all",
            async () => _mapper.Map<List<ServiceDTO>>(await _uow.Services.GetAllAsync())
        );

        var count = result?.Count ?? 0;
        span?.SetTag("services.count", count);
        _logger.LogInformation("Fetched {Count} services", count);

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<ServiceDTO?> GetByIdAsync(int id)
    {
        using var span = _activitySource.StartActivity("GetServiceById");
        span?.SetTag("service.id", id);

        _logger.LogInformation("Fetching service {ServiceId}", id);

        var result = await GetCachedAsync(
            $"services:{id}",
            async () =>
            {
                var service = await _uow.Services.GetByIdAsync(id);
                return service == null ? null : _mapper.Map<ServiceDTO>(service);
            }
        );

        if (result == null)
            _logger.LogWarning("Service {ServiceId} not found", id);

        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<ServiceDTO>> Create(ServiceDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateService");
        span?.SetTag("service.name", dto.Name);
        span?.SetTag("service.price", dto.Price);
        span?.SetTag("service.duration", dto.Duration);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating service {ServiceName} with price {Price} and duration {Duration}min",
            dto.Name, dto.Price, dto.Duration);

        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 3)
        {
            _logger.LogWarning("Service creation failed — invalid name: {ServiceName}", dto.Name);
            return Result<ServiceDTO>.Fail("Invalid Name");
        }

        if (dto.Duration <= 0)
        {
            _logger.LogWarning("Service creation failed — invalid duration: {Duration}", dto.Duration);
            return Result<ServiceDTO>.Fail("Invalid Duration");
        }

        if (dto.Price <= 0)
        {
            _logger.LogWarning("Service creation failed — invalid price: {Price}", dto.Price);
            return Result<ServiceDTO>.Fail("Invalid Price");
        }

        var entity = _mapper.Map<Service>(dto);

        await _uow.Services.AddAsync(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        stopwatch.Stop();

        span?.SetTag("service.id", entity.Id);
        _servicesCreated.Add(1);
        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "create" } });

        _logger.LogInformation(
            "Service {ServiceId} ({ServiceName}) created in {ElapsedMs}ms",
            entity.Id, entity.Name, stopwatch.Elapsed.TotalMilliseconds);

        return Result<ServiceDTO>.Ok(_mapper.Map<ServiceDTO>(entity));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<ServiceDTO>> Update(int id, ServiceDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateService");
        span?.SetTag("service.id", id);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Updating service {ServiceId}", id);

        var service = await _uow.Services.GetByIdAsync(id);

        if (service == null)
        {
            _logger.LogWarning("Service {ServiceId} not found for update", id);
            return Result<ServiceDTO>.Ok(null);
        }

        _mapper.Map(dto, service);
        service.LastUpdatedAt = DateTime.UtcNow;

        _uow.Services.Update(service);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        stopwatch.Stop();

        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "update" } });

        _logger.LogInformation(
            "Service {ServiceId} updated in {ElapsedMs}ms",
            id, stopwatch.Elapsed.TotalMilliseconds);

        return Result<ServiceDTO>.Ok(_mapper.Map<ServiceDTO>(service));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<ServiceDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteService");
        span?.SetTag("service.id", id);

        _logger.LogInformation("Deleting service {ServiceId}", id);

        var service = await _uow.Services.GetByIdAsync(id);

        if (service == null)
        {
            _logger.LogWarning("Service {ServiceId} not found for deletion", id);
            return Result<ServiceDTO>.Ok(null);
        }

        _uow.Services.Delete(service);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        _servicesDeleted.Add(1);

        _logger.LogInformation("Service {ServiceId} deleted successfully", id);

        return Result<ServiceDTO>.Ok(null);
    }
}