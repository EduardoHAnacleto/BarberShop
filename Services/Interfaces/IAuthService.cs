using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Services.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto);
    Task<Result<AuthResponseDTO>> GoogleLoginAsync(GoogleLoginDTO dto);
    Task<Result<bool>> UnlockUserAsync(int userId);
}
