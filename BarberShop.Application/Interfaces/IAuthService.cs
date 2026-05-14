using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto);
    Task<Result<AuthResponseDTO>> GoogleLoginAsync(GoogleLoginDTO dto);
    Task<Result<AuthResponseDTO>> RegisterAsync(RegisterDTO dto);
    Task<Result<bool>> UnlockUserAsync(int userId);
}
