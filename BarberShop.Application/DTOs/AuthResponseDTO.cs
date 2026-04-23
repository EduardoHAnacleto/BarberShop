namespace BarberShop.Application.DTOs;

public class AuthResponseDTO
{
    public string Token { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string UserRole { get; set; } = null!;
}
