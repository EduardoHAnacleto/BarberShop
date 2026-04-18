using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Services.Interfaces;

public interface IWorkingHoursService
{
    Task<bool> IsOpenAsync(DateTime dateTime);

    Task<List<BusinessScheduleDTO>> GetScheduleAsync();
    Task<BusinessScheduleDTO?> GetScheduleByDayAsync(DayOfWeek day);
    Task<Result<BusinessScheduleDTO>> UpdateScheduleAsync(int id, BusinessScheduleDTO dto);

    Task<List<WorkingHours>> GetClosuresAsync();
    Task<Result<WorkingHours>> AddClosureAsync(WorkingHours closure);
    Task<Result<bool>> RemoveClosureAsync(int id);
}
