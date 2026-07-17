using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IWorkerScheduleRepository : IRepository<WorkerSchedule>
{
    Task<WorkerSchedule?> GetByWorkerAndDayAsync(int workerId, DayOfWeek day);
    Task<List<WorkerSchedule>> GetByWorkerAsync(int workerId);
}
