using BarberShop.Models;

namespace BarberShop.Repositories.Interfaces;

public interface IBusinessScheduleRepository : IRepository<BusinessSchedule>
{
    Task<BusinessSchedule?> GetByDayAsync(DayOfWeek day);
    Task<List<BusinessSchedule>> GetAllOrderedAsync();
}
