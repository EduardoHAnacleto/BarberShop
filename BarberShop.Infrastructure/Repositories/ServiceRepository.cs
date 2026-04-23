using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;

namespace BarberShop.Infrastructure.Repositories;

public class ServiceRepository : GenericRepository<Service>,IServiceRepository
{
    public ServiceRepository(AppDbContext context) : base(context)
    {
    }
}
