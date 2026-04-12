using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;

namespace BarberShop.Repositories;

public class ServiceRepository : GenericRepository<Service>,IServiceRepository
{
    public ServiceRepository(AppDbContext context) : base(context)
    {
    }
}
