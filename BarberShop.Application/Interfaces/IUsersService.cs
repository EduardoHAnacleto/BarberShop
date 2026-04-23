using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IUsersService
{
    Task<List<UserResponseDTO>> GetAllAsync();
    Task<UserResponseDTO?> GetByIdAsync(int id);
    Task<Result<UserResponseDTO>> Create(UserRequestDTO dto);
    Task<Result<UserResponseDTO>> Update(int id, UserRequestDTO dto);
    Task<Result<UserResponseDTO>> Delete(int id);
}
