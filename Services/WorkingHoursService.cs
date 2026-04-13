using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace BarberShop.Services;

public class WorkingHoursService : IWorkingHoursService
{
    private readonly IWorkingHoursRepository _repository;
    private readonly BusinessHoursSettings _settings;

    public WorkingHoursService(IWorkingHoursRepository repository, IOptions<BusinessHoursSettings> options)
    {
        _repository = repository;
        _settings = options.Value;
    }
    public Task<List<Appointment>> CancelAppointments(DateTime fromTime, DateTime? untilTime)
    {
        throw new NotImplementedException();
    }

    public Task<List<Appointment>> DelayAppointments(TimeSpan time)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsOpenAsync(DateTime dateTime)
    {
        if (!IsWithinStandardBusinessHours(dateTime))
            return false;

        if (await IsInClosurePeriod(dateTime))
            return false;

        return true;
    }

    private bool IsWithinStandardBusinessHours(DateTime dateTime)
    {
        if (!_settings.Days.TryGetValue(dateTime.DayOfWeek, out var schedule))
            return false;

        if (!schedule.StartTime.HasValue || !schedule.EndTime.HasValue)
            return false;

        var time = dateTime.TimeOfDay;

        // Out of business hours
        if (time < schedule.StartTime || time > schedule.EndTime)
            return false;

        // Break/Lunch time
        if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue)
        {
            if (time >= schedule.BreakStart && time <= schedule.BreakEnd)
                return false;
        }

        return true;
    }

    private async Task<bool> IsInClosurePeriod(DateTime dateTime)
    {
        var closures = await _repository.GetAllAsync(
            w => w.ClosedFrom <= dateTime
        );

        foreach (var closure in closures)
        {
            var effectiveUntil = GetEffectiveClosedUntil(closure);

            if (dateTime >= closure.ClosedFrom && dateTime <= effectiveUntil)
                return true;
        }

        return false;
    }

    private DateTime GetEffectiveClosedUntil(WorkingHours closure)
    {
        return closure.ClosureType switch
        {
            ClosureType.UntilSpecificDate => closure.ClosedUntil
                ?? throw new Exception("ClosedUntil must be provided for UntilSpecificDate"),

            ClosureType.UntilNextOpening => GetNextOpening(closure.ClosedFrom)
                ?? throw new Exception("No next opening found"),

            _ => throw new Exception("Invalid closure type")
        };
    }

    private DateTime? GetNextOpening(DateTime from)
    {
        for (int i = 1; i <= 7; i++)
        {
            var nextDay = from.Date.AddDays(i);

            if (_settings.Days.TryGetValue(nextDay.DayOfWeek, out var schedule))
            {
                if (schedule.StartTime.HasValue)
                {
                    return nextDay.Add(schedule.StartTime.Value);
                }
            }
        }

        return null;
    }
}
