using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IWorkingHoursService
{
    // Standard business schedule
    Task<List<BusinessScheduleDTO>> GetScheduleAsync();
    Task<BusinessScheduleDTO?> GetScheduleByDayAsync(DayOfWeek day);
    Task<Result<BusinessScheduleDTO>> UpdateScheduleAsync(int id, BusinessScheduleDTO dto);

    // Closures
    Task<List<WorkingHours>> GetClosuresAsync();
    Task<Result<WorkingHours>> AddClosureAsync(ClosureDTO dto);
    Task<Result<bool>> RemoveClosureAsync(int id);

    // Closures resolved to concrete (From, Until) intervals that intersect the
    // given window — "UntilNextOpening" closures are expanded to their
    // effective end. Used by availability computation.
    Task<List<(DateTime From, DateTime Until)>> GetEffectiveClosuresAsync(
        DateTime windowStart, DateTime windowEnd);

    // Check if the business is open at a specific date and time
    Task<bool> IsOpenAsync(DateTime dateTime);
}
