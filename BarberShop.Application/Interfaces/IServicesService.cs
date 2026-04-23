using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IServicesService
{
    Task<List<ServiceDTO>> GetAllAsync();
    Task<ServiceDTO?> GetByIdAsync(int id);
    Task<Result<ServiceDTO>> Create(ServiceDTO dto);
    Task<Result<ServiceDTO>> Update(int id, ServiceDTO dto);
    Task<Result<ServiceDTO>> Delete(int id);
}
