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
    Task<Result<WorkingHours>> AddClosureAsync(WorkingHours closure);
    Task<Result<bool>> RemoveClosureAsync(int id);

    // Check if the business is open at a specific date and time
    Task<bool> IsOpenAsync(DateTime dateTime);
}
