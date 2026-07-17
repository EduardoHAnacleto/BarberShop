using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BarberShop.Application.Services;

public class WorkerScheduleService : BaseService, IWorkerScheduleService
{
    private static readonly ActivitySource _activitySource =
        new("BarberShop.WorkerScheduleService");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkerScheduleService> _logger;

    public WorkerScheduleService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        INotificationPublisher notifications,
        ILogger<WorkerScheduleService> logger) : base(redis, notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<WorkerScheduleDTO>> GetByWorkerAsync(int workerId)
    {
        using var span = _activitySource.StartActivity("GetWorkerSchedule");
        span?.SetTag("worker.id", workerId);

        var rows = await _uow.WorkerSchedules.GetByWorkerAsync(workerId);

        return _mapper.Map<List<WorkerScheduleDTO>>(rows);
    }

    public async Task<Result<WorkerScheduleDTO>> UpsertAsync(int workerId, DayOfWeek day, WorkerScheduleDTO dto)
    {
        using var span = _activitySource.StartActivity("UpsertWorkerSchedule");
        span?.SetTag("worker.id", workerId);
        span?.SetTag("schedule.day", day.ToString());
        span?.SetTag("schedule.isOpen", dto.IsOpen);

        var worker = await _uow.Workers.GetByIdAsync(workerId);
        if (worker == null)
        {
            _logger.LogWarning("UpsertWorkerSchedule failed — worker {WorkerId} not found", workerId);
            return Result<WorkerScheduleDTO>.Fail("Worker not found");
        }

        var validationError = ScheduleTimeValidator.Validate(
            dto.IsOpen, dto.OpenTime, dto.CloseTime, dto.BreakStart, dto.BreakEnd);
        if (validationError != null)
        {
            _logger.LogWarning(
                "UpsertWorkerSchedule rejected for worker {WorkerId} on {DayOfWeek}: {Reason}",
                workerId, day, validationError);
            return Result<WorkerScheduleDTO>.Fail(validationError);
        }

        var existing = await _uow.WorkerSchedules.GetByWorkerAndDayAsync(workerId, day);

        if (existing == null)
        {
            existing = new WorkerSchedule { WorkerId = workerId, DayOfWeek = day };
            await _uow.WorkerSchedules.AddAsync(existing);
        }

        existing.IsOpen = dto.IsOpen;
        existing.OpenTime = dto.OpenTime;
        existing.CloseTime = dto.CloseTime;
        existing.BreakStart = dto.BreakStart;
        existing.BreakEnd = dto.BreakEnd;

        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("worker-schedules", "WorkerSchedulesChanged");

        _logger.LogInformation(
            "Worker {WorkerId} schedule override for {DayOfWeek} saved — IsOpen: {IsOpen}",
            workerId, day, dto.IsOpen);

        return Result<WorkerScheduleDTO>.Ok(_mapper.Map<WorkerScheduleDTO>(existing));
    }

    public async Task<Result<bool>> RemoveOverrideAsync(int workerId, DayOfWeek day)
    {
        using var span = _activitySource.StartActivity("RemoveWorkerScheduleOverride");
        span?.SetTag("worker.id", workerId);
        span?.SetTag("schedule.day", day.ToString());

        var existing = await _uow.WorkerSchedules.GetByWorkerAndDayAsync(workerId, day);
        if (existing == null)
        {
            _logger.LogInformation(
                "No override to remove for worker {WorkerId} on {DayOfWeek}", workerId, day);
            return Result<bool>.Ok(false);
        }

        _uow.WorkerSchedules.Delete(existing);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("worker-schedules", "WorkerSchedulesChanged");

        _logger.LogInformation(
            "Worker {WorkerId} schedule override for {DayOfWeek} removed — reverted to shop default",
            workerId, day);

        return Result<bool>.Ok(true);
    }
}
