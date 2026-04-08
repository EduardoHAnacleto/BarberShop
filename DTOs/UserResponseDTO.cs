using BarberShop.Models;

namespace BarberShop.DTOs;

public class UserResponseDTO
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? WorkerId { get; set; }
    public string Email { get; set; } = null!;
    public UserRoles Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockoutEnd { get; set; }
}
