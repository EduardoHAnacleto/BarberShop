using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Services.Interfaces;

public interface IUsersService
{
    Task<List<UserResponseDTO>> GetAllAsync();
    Task<UserResponseDTO?> GetByIdAsync(int id);
    Task<Result<UserResponseDTO>> Create(UserRequestDTO dto);
    Task<Result<UserResponseDTO>> Update(int id, UserRequestDTO dto);
    Task<Result<UserResponseDTO>> Delete(int id);
}
