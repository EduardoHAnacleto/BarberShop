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

public class AppointmentsService : BaseService, IAppointmentsService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.AppointmentsService");

    private static readonly Meter _meter =
        new("BarberShop.AppointmentsService");

    private static readonly Counter<long> _appointmentsCreated =
        _meter.CreateCounter<long>(
            "barbershop.appointments.created",
            description: "Total number of appointments created");

    private static readonly Counter<long> _appointmentsCancelled =
        _meter.CreateCounter<long>(
            "barbershop.appointments.cancelled",
            description: "Total number of appointments cancelled");

    private static readonly Counter<long> _appointmentsDelayed =
        _meter.CreateCounter<long>(
            "barbershop.appointments.delayed",
            description: "Total number of appointments delayed");

    private static readonly Histogram<double> _operationDuration =
        _meter.CreateHistogram<double>(
            "barbershop.appointments.operation_duration",
            unit: "ms",
            description: "Duration of appointment operations");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<AppointmentsHub> _hub;
    private readonly ILogger<AppointmentsService> _logger;

    public AppointmentsService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<AppointmentsHub> hub,
        ILogger<AppointmentsService> logger) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
        _logger = logger;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllAppointments");

        _logger.LogInformation("Fetching all appointments");

        var result = await GetCachedAsync(
            "appointments:all",
            async () => _mapper.Map<List<AppointmentResponseDTO>>(
                await _uow.Appointments.GetAllAsync(
                    null,
                    q => q.OrderByDescending(a => a.ScheduledFor),
                    a => a.Customer,
                    a => a.Worker,
                    a => a.Service))
        );

        var count = result?.Count ?? 0;
        span?.SetTag("appointments.count", count);
        _logger.LogInformation("Fetched {Count} appointments", count);

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<AppointmentResponseDTO?> GetByIdAsync(int id)
    {
        using var span = _activitySource.StartActivity("GetAppointmentById");
        span?.SetTag("appointment.id", id);

        _logger.LogInformation("Fetching appointment {AppointmentId}", id);

        var result = await GetCachedAsync(
            $"appointments:{id}",
            async () =>
            {
                var entity = await _uow.Appointments.GetByIdAsync(id,
                    a => a.Customer,
                    a => a.Worker,
                    a => a.Service);

                return entity == null ? null : _mapper.Map<AppointmentResponseDTO>(entity);
            }
        );

        if (result == null)
            _logger.LogWarning("Appointment {AppointmentId} not found", id);

        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Create(AppointmentRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateAppointment");
        span?.SetTag("appointment.workerId", dto.WorkerId);
        span?.SetTag("appointment.customerId", dto.CustomerId);
        span?.SetTag("appointment.serviceId", dto.ServiceId);
        span?.SetTag("appointment.scheduledFor", dto.ScheduledFor.ToString("o"));

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating appointment for CustomerId {CustomerId} with WorkerId {WorkerId} on {ScheduledFor}",
            dto.CustomerId, dto.WorkerId, dto.ScheduledFor);

        var entity = _mapper.Map<Appointment>(dto);

        await _uow.Appointments.AddAsync(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        stopwatch.Stop();

        span?.SetTag("appointment.id", entity.Id);
        _appointmentsCreated.Add(1,
            new TagList
            {
                { "worker.id",  dto.WorkerId  },
                { "service.id", dto.ServiceId }
            });
        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "create" } });

        _logger.LogInformation(
            "Appointment {AppointmentId} created in {ElapsedMs}ms",
            entity.Id, stopwatch.Elapsed.TotalMilliseconds);

        return Result<AppointmentResponseDTO>.Ok(_mapper.Map<AppointmentResponseDTO>(entity));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Update(int id, AppointmentRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateAppointment");
        span?.SetTag("appointment.id", id);
        span?.SetTag("appointment.newStatus", dto.Status.ToString());

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Updating appointment {AppointmentId} to status {Status}",
            id, dto.Status);

        var entity = await _uow.Appointments.GetByIdAsync(id);

        if (entity == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found for update", id);
            return Result<AppointmentResponseDTO>.Ok(null);
        }

        var previousStatus = entity.Status;
        _mapper.Map(dto, entity);

        if (dto.Status == Status.Completed && entity.CompletedAt == null)
            entity.CompletedAt = DateTime.UtcNow;

        _uow.Appointments.Update(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        stopwatch.Stop();

        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "update" } });

        _logger.LogInformation(
            "Appointment {AppointmentId} updated from {PreviousStatus} to {NewStatus} in {ElapsedMs}ms",
            id, previousStatus, dto.Status, stopwatch.Elapsed.TotalMilliseconds);

        return Result<AppointmentResponseDTO>.Ok(_mapper.Map<AppointmentResponseDTO>(entity));
    }

    // =========================
    // DELETE (Virtual)
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteAppointment");
        span?.SetTag("appointment.id", id);

        _logger.LogInformation("Cancelling appointment {AppointmentId}", id);

        var entity = await _uow.Appointments.GetByIdAsync(id);

        if (entity == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", id);
            return Result<AppointmentResponseDTO>.Fail("Appointment not found");
        }

        if (entity.Status == Status.Cancelled)
        {
            _logger.LogWarning("Appointment {AppointmentId} already cancelled", id);
            return Result<AppointmentResponseDTO>.Fail("Already cancelled");
        }

        if (entity.Status == Status.Completed)
        {
            _logger.LogWarning("Appointment {AppointmentId} is completed, cannot cancel", id);
            return Result<AppointmentResponseDTO>.Fail("Completed cannot be cancelled");
        }

        if (entity.Status == Status.Deleted)
        {
            _logger.LogWarning("Appointment {AppointmentId} already deleted", id);
            return Result<AppointmentResponseDTO>.Fail("Already deleted");
        }

        await _uow.Appointments.VirtualDelete(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        _appointmentsCancelled.Add(1,
            new TagList
            {
                { "worker.id",  entity.WorkerId  },
                { "service.id", entity.ServiceId }
            });

        _logger.LogInformation("Appointment {AppointmentId} cancelled successfully", id);

        return Result<AppointmentResponseDTO>.Ok(null);
    }

    // =========================
    // FILTERS
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetByDateRange(DateTime start, DateTime end)
    {
        using var span = _activitySource.StartActivity("GetAppointmentsByDateRange");
        span?.SetTag("filter.start", start.ToString("o"));
        span?.SetTag("filter.end", end.ToString("o"));

        _logger.LogInformation(
            "Fetching appointments between {Start} and {End}", start, end);

        var data = await _uow.Appointments.GetAllAsync(
            a => a.ScheduledFor >= start && a.ScheduledFor <= end,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        span?.SetTag("appointments.count", data.Count);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByWorker(int workerId)
    {
        using var span = _activitySource.StartActivity("GetAppointmentsByWorker");
        span?.SetTag("filter.workerId", workerId);

        _logger.LogInformation("Fetching appointments for WorkerId {WorkerId}", workerId);

        var data = await _uow.Appointments.GetAllAsync(
            a => a.WorkerId == workerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        span?.SetTag("appointments.count", data.Count);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByCustomer(int customerId)
    {
        using var span = _activitySource.StartActivity("GetAppointmentsByCustomer");
        span?.SetTag("filter.customerId", customerId);

        _logger.LogInformation("Fetching appointments for CustomerId {CustomerId}", customerId);

        var data = await _uow.Appointments.GetAllAsync(
            a => a.CustomerId == customerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        span?.SetTag("appointments.count", data.Count);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByService(int serviceId)
    {
        using var span = _activitySource.StartActivity("GetAppointmentsByService");
        span?.SetTag("filter.serviceId", serviceId);

        _logger.LogInformation("Fetching appointments for ServiceId {ServiceId}", serviceId);

        var data = await _uow.Appointments.GetAllAsync(
            a => a.ServiceId == serviceId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        span?.SetTag("appointments.count", data.Count);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByStatus(Status status)
    {
        using var span = _activitySource.StartActivity("GetAppointmentsByStatus");
        span?.SetTag("filter.status", status.ToString());

        _logger.LogInformation("Fetching appointments with status {Status}", status);

        var data = await _uow.Appointments.GetAllAsync(
            a => a.Status == status,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        span?.SetTag("appointments.count", data.Count);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    // =========================
    // DELAY APPOINTMENTS
    // =========================
    public async Task<Result<List<AppointmentResponseDTO>>> DelayAppointments(
        List<int> idList, TimeSpan time)
    {
        using var span = _activitySource.StartActivity("DelayAppointments");
        span?.SetTag("appointments.count", idList?.Count ?? 0);
        span?.SetTag("delay.minutes", time.TotalMinutes);

        _logger.LogInformation(
            "Delaying {Count} appointments by {Minutes} minutes",
            idList?.Count ?? 0, time.TotalMinutes);

        if (idList == null || idList.Count == 0)
            return Result<List<AppointmentResponseDTO>>.Fail("No appointments provided");

        if (time <= TimeSpan.Zero)
            return Result<List<AppointmentResponseDTO>>.Fail("Delay time must be greater than zero");

        var tasks = idList.Select(async id =>
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found for delay", id);
                return null;
            }

            entity.ScheduledFor = entity.ScheduledFor.Add(time);
            entity.LastUpdatedAt = DateTime.UtcNow;
            _uow.Appointments.Update(entity);

            return _mapper.Map<AppointmentResponseDTO>(entity);
        });

        var results = (await Task.WhenAll(tasks))
            .Where(x => x != null)
            .ToList();

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        _appointmentsDelayed.Add(results.Count,
            new TagList { { "delay.minutes", time.TotalMinutes } });

        _logger.LogInformation(
            "{Delayed} of {Total} appointments delayed by {Minutes} minutes",
            results.Count, idList.Count, time.TotalMinutes);

        return Result<List<AppointmentResponseDTO>>.Ok(results!);
    }

    // =========================
    // CANCEL APPOINTMENTS (BATCH)
    // =========================
    public async Task<Result<List<AppointmentResponseDTO>>> CancelAppointments(List<int> idList)
    {
        using var span = _activitySource.StartActivity("CancelAppointments");
        span?.SetTag("appointments.count", idList?.Count ?? 0);

        _logger.LogInformation("Batch cancelling {Count} appointments", idList?.Count ?? 0);

        if (idList == null || idList.Count == 0)
            return Result<List<AppointmentResponseDTO>>.Fail("No appointments provided");

        var tasks = idList.Select(async id =>
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity == null)
            {
                _logger.LogWarning(
                    "Appointment {AppointmentId} not found for batch cancel", id);
                return null;
            }

            await _uow.Appointments.VirtualDelete(entity);

            return _mapper.Map<AppointmentResponseDTO>(entity);
        });

        var results = (await Task.WhenAll(tasks))
            .Where(x => x != null)
            .ToList();

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        _appointmentsCancelled.Add(results.Count,
            new TagList { { "source", "batch" } });

        _logger.LogInformation(
            "{Cancelled} of {Total} appointments cancelled",
            results.Count, idList.Count);

        return Result<List<AppointmentResponseDTO>>.Ok(results!);
    }
}