using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;

namespace BarberShop.Repositories;

public class WorkingHoursRepository : GenericRepository<WorkingHours>, IWorkingHoursRepository
{
    public WorkingHoursRepository(AppDbContext context) : base(context)
    {
        
    }

}
