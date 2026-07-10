using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface ILoyaltyService
{
    Task<LoyaltyStatusDTO> GetStatusAsync(int customerId);
}
