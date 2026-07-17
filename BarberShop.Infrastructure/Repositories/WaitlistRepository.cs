using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;

namespace BarberShop.Infrastructure.Repositories;

public class WaitlistRepository : GenericRepository<Waitlist>, IWaitlistRepository
{
    public WaitlistRepository(AppDbContext context) : base(context)
    {
    }
}
