using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;

namespace BarberShop.Infrastructure.Repositories;

public class WorkingHoursRepository : GenericRepository<WorkingHours>, IWorkingHoursRepository
{
    public WorkingHoursRepository(AppDbContext context) : base(context)
    {
        
    }

}
