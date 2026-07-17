using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BarberShop.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private static readonly ActivitySource _activitySource =
        new("BarberShop.AvailabilityService");

    // Slot grid step and the minimum notice required to book a same-day slot.
    // Mirrors the values the booking UI has always used.
    private const int SlotIntervalMinutes = 30;
    private const int MinLeadTimeMinutes = 10;

    private readonly IUnitOfWork _uow;
    private readonly IWorkingHoursService _workingHours;
    private readonly IShopClock _clock;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(
        IUnitOfWork uow,
        IWorkingHoursService workingHours,
        IShopClock clock,
        ILogger<AvailabilityService> logger)
    {
        _uow = uow;
        _workingHours = workingHours;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AvailabilityResponseDTO>> GetAvailabilityAsync(
        int workerId, DateOnly date, int serviceId)
    {
        using var span = _activitySource.StartActivity("GetAvailability");
        span?.SetTag("worker.id", workerId);
        span?.SetTag("service.id", serviceId);
        span?.SetTag("date", date.ToString("O"));

        var worker = await _uow.Workers.GetByIdAsync(workerId);
        if (worker == null)
            return Result<AvailabilityResponseDTO>.Fail("Worker not found");

        var service = await _uow.Services.GetByIdAsync(serviceId);
        if (service == null)
            return Result<AvailabilityResponseDTO>.Fail("Service not found");

        var response = new AvailabilityResponseDTO
        {
            WorkerId = workerId,
            ServiceId = serviceId,
            Date = date.ToString("yyyy-MM-dd"),
        };

        var now = _clock.Now;

        // Whole day already in the past → nothing to offer.
        if (date < DateOnly.FromDateTime(now))
        {
            response.IsOpen = false;
            return Result<AvailabilityResponseDTO>.Ok(response);
        }

        // The shop must be open that weekday at all — a per-worker override
        // can narrow hours or take a worker off a day the shop is open, but
        // it can never make a worker available on a day the shop is closed.
        var shopSchedule = await _workingHours.GetScheduleByDayAsync(date.DayOfWeek);
        if (shopSchedule is not { IsOpen: true } ||
            !shopSchedule.OpenTime.HasValue || !shopSchedule.CloseTime.HasValue)
        {
            response.IsOpen = false;
            return Result<AvailabilityResponseDTO>.Ok(response);
        }

        var workerOverride = await _uow.WorkerSchedules.GetByWorkerAndDayAsync(workerId, date.DayOfWeek);
        var schedule = workerOverride == null
            ? shopSchedule
            : new BusinessScheduleDTO
            {
                IsOpen = workerOverride.IsOpen,
                OpenTime = workerOverride.OpenTime,
                CloseTime = workerOverride.CloseTime,
                BreakStart = workerOverride.BreakStart,
                BreakEnd = workerOverride.BreakEnd,
            };

        if (schedule is not { IsOpen: true } ||
            !schedule.OpenTime.HasValue || !schedule.CloseTime.HasValue)
        {
            response.IsOpen = false;
            return Result<AvailabilityResponseDTO>.Ok(response);
        }

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

        // Blocking intervals for the day, all as [start, end) DateTime pairs:
        // exceptional closures, the lunch break, and active appointments
        // (each occupying its own service's duration).
        var blocked = (await _workingHours.GetEffectiveClosuresAsync(dayStart, dayEnd))
            .Select(c => (From: c.From, Until: c.Until))
            .ToList();

        if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue)
            blocked.Add((
                date.ToDateTime(TimeOnly.FromTimeSpan(schedule.BreakStart.Value)),
                date.ToDateTime(TimeOnly.FromTimeSpan(schedule.BreakEnd.Value))));

        var appointments = await _uow.Appointments.GetByWorker(workerId) ?? [];

        blocked.AddRange(appointments
            .Where(a => a.Status is Status.Scheduled or Status.OnGoing)
            .Where(a => DateOnly.FromDateTime(a.ScheduledFor) == date)
            .Select(a => (
                From: a.ScheduledFor,
                Until: a.ScheduledFor.AddMinutes(a.Service?.Duration ?? SlotIntervalMinutes))));

        // Same-day bookings require a minimum notice.
        var cutoff = DateOnly.FromDateTime(now) == date
            ? now.AddMinutes(MinLeadTimeMinutes)
            : (DateTime?)null;

        var open = date.ToDateTime(TimeOnly.FromTimeSpan(schedule.OpenTime.Value));
        var close = date.ToDateTime(TimeOnly.FromTimeSpan(schedule.CloseTime.Value));

        for (var slot = open; slot < close; slot = slot.AddMinutes(SlotIntervalMinutes))
        {
            var slotEnd = slot.AddMinutes(service.Duration);

            // The full service must fit before closing time.
            if (slotEnd > close) continue;

            // Same-day lead time: slot must start strictly after the cutoff.
            if (cutoff.HasValue && slot <= cutoff.Value) continue;

            // Two half-open intervals [a,b) and [c,d) overlap iff a < d && c < b.
            var overlaps = blocked.Any(b => slot < b.Until && b.From < slotEnd);
            if (overlaps) continue;

            response.Slots.Add(slot.ToString("HH:mm"));
        }

        span?.SetTag("slots.count", response.Slots.Count);
        _logger.LogInformation(
            "Availability for worker {WorkerId} on {Date} (service {ServiceId}): {Count} slots",
            workerId, response.Date, serviceId, response.Slots.Count);

        return Result<AvailabilityResponseDTO>.Ok(response);
    }
}
