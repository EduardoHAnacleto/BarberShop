using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Infrastructure.Repositories;

public class WorkerScheduleRepository : GenericRepository<WorkerSchedule>, IWorkerScheduleRepository
{
    public WorkerScheduleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<WorkerSchedule?> GetByWorkerAndDayAsync(int workerId, DayOfWeek day)
        => await _dbSet.FirstOrDefaultAsync(s => s.WorkerId == workerId && s.DayOfWeek == day);

    public async Task<List<WorkerSchedule>> GetByWorkerAsync(int workerId)
        => await _dbSet.Where(s => s.WorkerId == workerId).OrderBy(s => s.DayOfWeek).ToListAsync();
}
