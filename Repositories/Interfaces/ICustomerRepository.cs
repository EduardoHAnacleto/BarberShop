using BarberShop.Models;

namespace BarberShop.Repositories.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email);
}
