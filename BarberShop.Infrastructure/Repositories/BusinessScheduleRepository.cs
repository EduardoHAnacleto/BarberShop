using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Infrastructure.Repositories;

public class BusinessScheduleRepository : GenericRepository<BusinessSchedule>, IBusinessScheduleRepository
{
    public BusinessScheduleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<BusinessSchedule?> GetByDayAsync(DayOfWeek day)
        => await _dbSet.FirstOrDefaultAsync(s => s.DayOfWeek == day);

    public async Task<List<BusinessSchedule>> GetAllOrderedAsync()
        => await _dbSet.OrderBy(s => s.DayOfWeek).ToListAsync();
}
