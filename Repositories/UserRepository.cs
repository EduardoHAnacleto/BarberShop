using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;

namespace BarberShop.Repositories;

public class UserRepository : GenericRepository<User> ,IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }
}
