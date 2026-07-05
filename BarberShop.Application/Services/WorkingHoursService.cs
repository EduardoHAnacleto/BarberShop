using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BarberShop.Application.Services;

public class WorkingHoursService : IWorkingHoursService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.WorkingHoursService");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkingHoursService> _logger;

    public WorkingHoursService(
        IUnitOfWork uow,
        IMapper mapper,
        ILogger<WorkingHoursService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // =========================
    // SCHEDULE
    // =========================
    public async Task<List<BusinessScheduleDTO>> GetScheduleAsync()
    {
        using var span = _activitySource.StartActivity("GetSchedule");

        _logger.LogInformation("Fetching full business schedule");

        var schedules = await _uow.BusinessSchedules.GetAllOrderedAsync();

        span?.SetTag("schedule.count", schedules.Count);

        return _mapper.Map<List<BusinessScheduleDTO>>(schedules);
    }

    public async Task<BusinessScheduleDTO?> GetScheduleByDayAsync(DayOfWeek day)
    {
        using var span = _activitySource.StartActivity("GetScheduleByDay");
        span?.SetTag("schedule.day", day.ToString());

        _logger.LogInformation("Fetching schedule for {DayOfWeek}", day);

        var schedule = await _uow.BusinessSchedules.GetByDayAsync(day);

        if (schedule == null)
        {
            _logger.LogWarning("No schedule found for {DayOfWeek}", day);
            return null;
        }

        return _mapper.Map<BusinessScheduleDTO>(schedule);
    }

    public async Task<Result<BusinessScheduleDTO>> UpdateScheduleAsync(
        int id, BusinessScheduleDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateSchedule");
        span?.SetTag("schedule.id", id);
        span?.SetTag("schedule.day", dto.DayOfWeek.ToString());
        span?.SetTag("schedule.isOpen", dto.IsOpen);

        _logger.LogInformation(
            "Updating schedule {ScheduleId} for {DayOfWeek} — IsOpen: {IsOpen}",
            id, dto.DayOfWeek, dto.IsOpen);

        var schedule = await _uow.BusinessSchedules.GetByIdAsync(id);

        if (schedule == null)
        {
            _logger.LogWarning("Schedule {ScheduleId} not found", id);
            return Result<BusinessScheduleDTO>.Fail("Schedule not found");
        }

        _mapper.Map(dto, schedule);

        _uow.BusinessSchedules.Update(schedule);
        await _uow.SaveAsync();

        _logger.LogInformation("Schedule {ScheduleId} updated successfully", id);

        return Result<BusinessScheduleDTO>.Ok(_mapper.Map<BusinessScheduleDTO>(schedule));
    }

    // =========================
    // CLOSURES
    // =========================
    public async Task<List<WorkingHours>> GetClosuresAsync()
    {
        using var span = _activitySource.StartActivity("GetClosures");

        _logger.LogInformation("Fetching all closures");

        var closures = await _uow.WorkingHours.GetAllAsync();

        span?.SetTag("closures.count", closures.Count);

        return closures;
    }

    public async Task<Result<WorkingHours>> AddClosureAsync(ClosureDTO dto)
    {
        using var span = _activitySource.StartActivity("AddClosure");
        span?.SetTag("closure.reason", dto.Reason);
        span?.SetTag("closure.type", dto.ClosureType.ToString());
        span?.SetTag("closure.from", dto.ClosedFrom.ToString("o"));

        _logger.LogInformation(
            "Adding closure from {ClosedFrom} — reason: {Reason}",
            dto.ClosedFrom, dto.Reason);

        if (dto.ClosureType == ClosureType.UntilSpecificDate && !dto.ClosedUntil.HasValue)
        {
            _logger.LogWarning("AddClosure failed — ClosedUntil is required for UntilSpecificDate closure");
            return Result<WorkingHours>.Fail("ClosedUntil is required when ClosureType is UntilSpecificDate.");
        }

        var closure = _mapper.Map<WorkingHours>(dto);

        await _uow.WorkingHours.AddAsync(closure);
        await _uow.SaveAsync();

        _logger.LogInformation("Closure {ClosureId} added successfully", closure.Id);

        return Result<WorkingHours>.Ok(closure);
    }

    public async Task<Result<bool>> RemoveClosureAsync(int id)
    {
        using var span = _activitySource.StartActivity("RemoveClosure");
        span?.SetTag("closure.id", id);

        _logger.LogInformation("Removing closure {ClosureId}", id);

        var closure = await _uow.WorkingHours.GetByIdAsync(id);

        if (closure == null)
        {
            _logger.LogWarning("Closure {ClosureId} not found", id);
            return Result<bool>.Ok(false);
        }

        _uow.WorkingHours.Delete(closure);
        await _uow.SaveAsync();

        _logger.LogInformation("Closure {ClosureId} removed successfully", id);

        return Result<bool>.Ok(true);
    }

    // =========================
    // IS OPEN
    // =========================
    public async Task<bool> IsOpenAsync(DateTime dateTime)
    {
        using var span = _activitySource.StartActivity("IsOpen");
        span?.SetTag("datetime", dateTime.ToString("o"));

        _logger.LogInformation("Checking if business is open at {DateTime}", dateTime);

        var schedule = await _uow.BusinessSchedules.GetByDayAsync(dateTime.DayOfWeek);

        if (schedule == null || !schedule.IsOpen)
        {
            _logger.LogInformation(
                "Business is closed at {DateTime} — no schedule or day marked closed",
                dateTime);
            span?.SetTag("is_open", false);
            return false;
        }

        if (!IsWithinSchedule(dateTime.TimeOfDay, schedule))
        {
            _logger.LogInformation(
                "Business is closed at {DateTime} — outside business hours", dateTime);
            span?.SetTag("is_open", false);
            return false;
        }

        if (await IsInClosurePeriod(dateTime))
        {
            _logger.LogInformation(
                "Business is closed at {DateTime} — exceptional closure active", dateTime);
            span?.SetTag("is_open", false);
            return false;
        }

        _logger.LogInformation("Business is open at {DateTime}", dateTime);
        span?.SetTag("is_open", true);
        return true;
    }

    private bool IsWithinSchedule(TimeSpan time, BusinessSchedule schedule)
    {
        if (!schedule.OpenTime.HasValue || !schedule.CloseTime.HasValue)
            return false;

        if (time < schedule.OpenTime || time > schedule.CloseTime)
            return false;

        if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue)
            if (time >= schedule.BreakStart && time <= schedule.BreakEnd)
                return false;

        return true;
    }

    private async Task<bool> IsInClosurePeriod(DateTime dateTime)
    {
        var closures = await _uow.WorkingHours.GetAllAsync(
            w => w.ClosedFrom <= dateTime);

        foreach (var closure in closures)
        {
            var effectiveUntil = await GetEffectiveClosedUntil(closure);

            if (effectiveUntil.HasValue && dateTime >= closure.ClosedFrom && dateTime <= effectiveUntil.Value)
                return true;
        }

        return false;
    }

    private async Task<DateTime?> GetEffectiveClosedUntil(WorkingHours closure)
    {
        switch (closure.ClosureType)
        {
            case ClosureType.UntilSpecificDate:
                return closure.ClosedUntil;

            case ClosureType.UntilNextOpening:
                var nextOpening = await GetNextOpeningAsync(closure.ClosedFrom);
                if (nextOpening is null)
                {
                    _logger.LogWarning(
                        "Closure {ClosureId} (UntilNextOpening) found no opening in the next 7 days — treating as indefinitely closed",
                        closure.Id);
                    return DateTime.MaxValue;
                }
                return nextOpening;

            default:
                _logger.LogWarning(
                    "Closure {ClosureId} has unrecognized ClosureType {ClosureType} — closure will be ignored",
                    closure.Id, (int)closure.ClosureType);
                return null;
        }
    }

    private async Task<DateTime?> GetNextOpeningAsync(DateTime from)
    {
        for (int i = 1; i <= 7; i++)
        {
            var nextDay = from.Date.AddDays(i);
            var schedule = await _uow.BusinessSchedules.GetByDayAsync(nextDay.DayOfWeek);

            if (schedule is { IsOpen: true, OpenTime: not null })
                return nextDay.Add(schedule.OpenTime.Value);
        }

        return null;
    }
}