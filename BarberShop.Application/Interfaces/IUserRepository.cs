using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
