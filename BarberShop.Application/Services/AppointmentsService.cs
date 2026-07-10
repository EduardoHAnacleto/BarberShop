using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
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
    private readonly ILogger<AppointmentsService> _logger;

    public AppointmentsService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        INotificationPublisher notifications,
        ILogger<AppointmentsService> logger) : base(redis, notifications)
    {
        _uow = uow;
        _mapper = mapper;
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
    // GET ALL PAGED
    // =========================
    public async Task<PagedResult<AppointmentResponseDTO>> GetAllAsync(PaginationParams pagination)
    {
        using var span = _activitySource.StartActivity("GetAllAppointmentsPaged");
        span?.SetTag("pagination.page", pagination.Page);
        span?.SetTag("pagination.pageSize", pagination.PageSize);

        _logger.LogInformation(
            "Fetching appointments page {Page} (size {PageSize})",
            pagination.Page, pagination.PageSize);

        var paged = await _uow.Appointments.GetPagedAsync(
            pagination,
            filter: null,
            orderBy: q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        var mapped = PagedResult<AppointmentResponseDTO>.Create(
            _mapper.Map<List<AppointmentResponseDTO>>(paged.Items),
            paged.TotalCount,
            pagination);

        span?.SetTag("appointments.totalCount", mapped.TotalCount);
        span?.SetTag("appointments.totalPages", mapped.TotalPages);

        _logger.LogInformation(
            "Fetched page {Page}/{TotalPages} ({TotalCount} total appointments)",
            mapped.Page, mapped.TotalPages, mapped.TotalCount);

        return mapped;
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

        // Server-side conflict guard: even though the booking UI only offers
        // free slots, two clients can race for the same one. Reject overlaps
        // for the same worker so the double-booking never reaches the database.
        if (await HasConflictAsync(dto.WorkerId, dto.ServiceId, dto.ScheduledFor))
        {
            _logger.LogWarning(
                "Appointment creation rejected — slot taken for WorkerId {WorkerId} at {ScheduledFor}",
                dto.WorkerId, dto.ScheduledFor);
            return Result<AppointmentResponseDTO>.Fail("This time slot is no longer available");
        }

        var entity = _mapper.Map<Appointment>(dto);

        await _uow.Appointments.AddAsync(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

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
    // CREATE RECURRING
    // =========================
    // Books up to RepeatWeeks occurrences 7 days apart under one RecurrenceId.
    // Each occurrence is validated independently against HasConflictAsync —
    // weekly spacing means occurrences can never overlap each other, only a
    // pre-existing booking — so a conflicting week is skipped rather than
    // failing the whole series.
    public async Task<Result<RecurringAppointmentResultDTO>> CreateRecurring(RecurringAppointmentRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateRecurringAppointments");
        span?.SetTag("appointment.workerId", dto.WorkerId);
        span?.SetTag("appointment.repeatWeeks", dto.RepeatWeeks);

        if (dto.RepeatWeeks < 1 || dto.RepeatWeeks > 12)
            return Result<RecurringAppointmentResultDTO>.Fail("RepeatWeeks must be between 1 and 12");

        // Resolved once up front: needed to validate the ids are real and to
        // attach to every occurrence so the response mapping (WorkerName /
        // CustomerName / ServiceName) doesn't depend on EF change-tracker
        // fixup picking them up by accident.
        var worker = await _uow.Workers.GetByIdAsync(dto.WorkerId);
        if (worker == null)
            return Result<RecurringAppointmentResultDTO>.Fail("Worker not found");

        var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId);
        if (customer == null)
            return Result<RecurringAppointmentResultDTO>.Fail("Customer not found");

        var service = await _uow.Services.GetByIdAsync(dto.ServiceId);
        if (service == null)
            return Result<RecurringAppointmentResultDTO>.Fail("Service not found");

        var recurrenceId = Guid.NewGuid();
        var toCreate = new List<Appointment>();
        var skipped = new List<DateTime>();

        for (var i = 0; i < dto.RepeatWeeks; i++)
        {
            var occurrenceDate = dto.ScheduledFor.AddDays(7 * i);

            if (await HasConflictAsync(dto.WorkerId, dto.ServiceId, occurrenceDate))
            {
                skipped.Add(occurrenceDate);
                continue;
            }

            toCreate.Add(new Appointment
            {
                WorkerId = dto.WorkerId,
                Worker = worker,
                CustomerId = dto.CustomerId,
                Customer = customer,
                ServiceId = dto.ServiceId,
                Service = service,
                ScheduledFor = occurrenceDate,
                Status = Status.Scheduled,
                ExtraDetails = dto.ExtraDetails,
                RecurrenceId = recurrenceId,
            });
        }

        if (toCreate.Count == 0)
        {
            _logger.LogWarning(
                "Recurring appointment creation rejected — every occurrence conflicted for WorkerId {WorkerId}",
                dto.WorkerId);
            return Result<RecurringAppointmentResultDTO>.Fail("All occurrences conflict with existing appointments");
        }

        foreach (var entity in toCreate)
            await _uow.Appointments.AddAsync(entity);

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

        _appointmentsCreated.Add(toCreate.Count,
            new TagList
            {
                { "worker.id",  dto.WorkerId  },
                { "service.id", dto.ServiceId },
                { "recurring",  true          },
            });

        _logger.LogInformation(
            "Recurring series {RecurrenceId} created {Created}/{Requested} occurrences for WorkerId {WorkerId}",
            recurrenceId, toCreate.Count, dto.RepeatWeeks, dto.WorkerId);

        return Result<RecurringAppointmentResultDTO>.Ok(new RecurringAppointmentResultDTO
        {
            RecurrenceId = recurrenceId,
            Created = _mapper.Map<List<AppointmentResponseDTO>>(toCreate),
            SkippedDates = skipped,
        });
    }

    // Returns true when a proposed booking for [scheduledFor, +serviceDuration)
    // overlaps any active (Scheduled/OnGoing) appointment of the same worker.
    // excludeAppointmentId lets an update ignore the row being edited.
    private async Task<bool> HasConflictAsync(
        int workerId, int serviceId, DateTime scheduledFor, int? excludeAppointmentId = null)
    {
        var service = await _uow.Services.GetByIdAsync(serviceId);
        if (service == null)
            return false; // Missing service is validated elsewhere.

        var proposedStart = scheduledFor;
        var proposedEnd = scheduledFor.AddMinutes(service.Duration);

        var existing = await _uow.Appointments.GetByWorker(workerId) ?? [];

        // Two half-open intervals [a,b) and [c,d) overlap iff a < d && c < b.
        return existing.Any(a =>
            a.Id != excludeAppointmentId &&
            a.Status is Status.Scheduled or Status.OnGoing &&
            proposedStart < a.ScheduledFor.AddMinutes(a.Service?.Duration ?? service.Duration) &&
            a.ScheduledFor < proposedEnd);
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

        // A reschedule (changed date/time) must not collide with another of the
        // worker's active bookings. Exclude this appointment so its own current
        // slot never counts as a conflict against itself.
        if (dto.ScheduledFor != entity.ScheduledFor &&
            await HasConflictAsync(dto.WorkerId, dto.ServiceId, dto.ScheduledFor, id))
        {
            _logger.LogWarning(
                "Appointment {AppointmentId} reschedule rejected — slot taken at {ScheduledFor}",
                id, dto.ScheduledFor);
            return Result<AppointmentResponseDTO>.Fail("This time slot is no longer available");
        }

        var previousStatus = entity.Status;
        _mapper.Map(dto, entity);

        if (dto.Status == Status.Completed && entity.CompletedAt == null)
            entity.CompletedAt = DateTime.UtcNow;

        _uow.Appointments.Update(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

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
    // CHANGE STATUS
    // =========================
    // Focused status transition for the worker portal (start / complete /
    // no-show) without requiring the full appointment payload. Completing an
    // appointment stamps CompletedAt once.
    public async Task<Result<AppointmentResponseDTO>> ChangeStatus(int id, Status status)
    {
        using var span = _activitySource.StartActivity("ChangeAppointmentStatus");
        span?.SetTag("appointment.id", id);
        span?.SetTag("appointment.newStatus", status.ToString());

        var entity = await _uow.Appointments.GetByIdAsync(id);

        if (entity == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found for status change", id);
            return Result<AppointmentResponseDTO>.Ok(null);
        }

        entity.Status = status;

        if (status == Status.Completed && entity.CompletedAt == null)
            entity.CompletedAt = DateTime.UtcNow;

        _uow.Appointments.Update(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

        _logger.LogInformation(
            "Appointment {AppointmentId} status changed to {Status}", id, status);

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
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

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

        var results = new List<AppointmentResponseDTO?>();
        foreach (var id in idList)
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Appointment {AppointmentId} not found for delay", id);
                continue;
            }

            entity.ScheduledFor = entity.ScheduledFor.Add(time);
            entity.LastUpdatedAt = DateTime.UtcNow;
            _uow.Appointments.Update(entity);

            results.Add(_mapper.Map<AppointmentResponseDTO>(entity));
        }

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

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

        var results = new List<AppointmentResponseDTO?>();
        foreach (var id in idList)
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity == null)
            {
                _logger.LogWarning(
                    "Appointment {AppointmentId} not found for batch cancel", id);
                continue;
            }

            if (entity.Status == Status.Cancelled || entity.Status == Status.Completed)
            {
                _logger.LogWarning(
                    "Appointment {AppointmentId} skipped in batch cancel — status is {Status}",
                    id, entity.Status);
                continue;
            }

            entity.Status = Status.Cancelled;
            entity.CompletedAt = DateTime.UtcNow;
            _uow.Appointments.Update(entity);
            results.Add(_mapper.Map<AppointmentResponseDTO>(entity));
        }

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", "AppointmentsChanged");

        _appointmentsCancelled.Add(results.Count,
            new TagList { { "source", "batch" } });

        _logger.LogInformation(
            "{Cancelled} of {Total} appointments cancelled",
            results.Count, idList.Count);

        return Result<List<AppointmentResponseDTO>>.Ok(results!);
    }
}