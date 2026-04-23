using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface ICustomersService
{
    Task<List<CustomerDTO>> GetAllAsync();
    Task<CustomerDTO?> GetByIdAsync(int id);
    Task<Result<CustomerDTO>> Create(CustomerDTO dto);
    Task<Result<CustomerDTO>> Update(int id, CustomerDTO dto);
    Task<Result<CustomerDTO>> Delete(int id);
}
