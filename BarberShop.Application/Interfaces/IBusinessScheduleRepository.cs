using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IBusinessScheduleRepository : IRepository<BusinessSchedule>
{
    Task<BusinessSchedule?> GetByDayAsync(DayOfWeek day);
    Task<List<BusinessSchedule>> GetAllOrderedAsync();
}
