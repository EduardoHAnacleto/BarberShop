using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace BarberShop.Services;

public class WorkingHoursService : IWorkingHoursService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public WorkingHoursService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    // =========================
    // SCHEDULE (horário padrão)
    // =========================
    public async Task<List<BusinessScheduleDTO>> GetScheduleAsync()
    {
        var schedules = await _uow.BusinessSchedules.GetAllOrderedAsync();
        return _mapper.Map<List<BusinessScheduleDTO>>(schedules);
    }

    public async Task<BusinessScheduleDTO?> GetScheduleByDayAsync(DayOfWeek day)
    {
        var schedule = await _uow.BusinessSchedules.GetByDayAsync(day);
        return schedule == null ? null : _mapper.Map<BusinessScheduleDTO>(schedule);
    }

    public async Task<Result<BusinessScheduleDTO>> UpdateScheduleAsync(int id, BusinessScheduleDTO dto)
    {
        var schedule = await _uow.BusinessSchedules.GetByIdAsync(id);

        if (schedule == null)
            return Result<BusinessScheduleDTO>.Fail("Schedule not found");

        _mapper.Map(dto, schedule);

        _uow.BusinessSchedules.Update(schedule);
        await _uow.SaveAsync();

        return Result<BusinessScheduleDTO>.Ok(_mapper.Map<BusinessScheduleDTO>(schedule));
    }

    // =========================
    // CLOSURES (fechamentos excepcionais)
    // =========================
    public async Task<List<WorkingHours>> GetClosuresAsync()
        => await _uow.WorkingHours.GetAllAsync();

    public async Task<Result<WorkingHours>> AddClosureAsync(WorkingHours closure)
    {
        await _uow.WorkingHours.AddAsync(closure);
        await _uow.SaveAsync();
        return Result<WorkingHours>.Ok(closure);
    }

    public async Task<Result<bool>> RemoveClosureAsync(int id)
    {
        var closure = await _uow.WorkingHours.GetByIdAsync(id);

        if (closure == null)
            return Result<bool>.Ok(false);

        _uow.WorkingHours.Delete(closure);
        await _uow.SaveAsync();
        return Result<bool>.Ok(true);
    }

    // =========================
    // IS OPEN
    // =========================
    public async Task<bool> IsOpenAsync(DateTime dateTime)
    {
        var schedule = await _uow.BusinessSchedules.GetByDayAsync(dateTime.DayOfWeek);

        if (schedule == null || !schedule.IsOpen)
            return false;

        if (!IsWithinSchedule(dateTime.TimeOfDay, schedule))
            return false;

        if (await IsInClosurePeriod(dateTime))
            return false;

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

            if (dateTime >= closure.ClosedFrom && dateTime <= effectiveUntil)
                return true;
        }

        return false;
    }

    private async Task<DateTime> GetEffectiveClosedUntil(WorkingHours closure)
    {
        return closure.ClosureType switch
        {
            ClosureType.UntilSpecificDate => closure.ClosedUntil
                ?? throw new Exception("ClosedUntil must be provided for UntilSpecificDate"),

            ClosureType.UntilNextOpening => await GetNextOpeningAsync(closure.ClosedFrom)
                ?? throw new Exception("No next opening found"),

            _ => throw new Exception("Invalid closure type")
        };
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