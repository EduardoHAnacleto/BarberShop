using BarberShop.Domain.Enums;

namespace BarberShop.Application.DTOs;

public class UserRequestDTO
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? WorkerId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRoles UserRole { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockoutEnd { get; set; }
}
