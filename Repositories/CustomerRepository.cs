using BarberShop.Data;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Repositories;

public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.Email == email);
    }


}
