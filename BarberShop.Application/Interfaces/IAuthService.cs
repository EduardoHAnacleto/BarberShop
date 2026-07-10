using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto);
    Task<Result<AuthResponseDTO>> GoogleLoginAsync(GoogleLoginDTO dto);
    Task<Result<AuthResponseDTO>> RegisterAsync(RegisterDTO dto);
    Task<Result<bool>> UnlockUserAsync(int userId);
    Task<Result<bool>> LogoutAsync(int userId);
    Task<Result<bool>> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

    // Always succeeds from the caller's perspective (never reveals whether the
    // email exists) — the controller returns the same generic message either way.
    Task ForgotPasswordAsync(string email);
    Task<Result<bool>> ResetPasswordAsync(string token, string newPassword);
}
