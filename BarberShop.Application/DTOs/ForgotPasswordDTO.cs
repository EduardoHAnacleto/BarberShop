namespace BarberShop.Application.DTOs;

public class ForgotPasswordDTO
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDTO
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
