using BarberShop.Domain.Enums;

namespace BarberShop.Domain.Models;

public class User
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRoles UserRole { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public string? GoogleId { get; set; }

    // Opaque revocation token embedded in every JWT as the "stamp" claim.
    // Rotating it (on logout or credential change) invalidates all tokens
    // previously issued for this user, even though the JWT itself has a
    // long expiry.
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    // Single-use forgot-password token and its expiry; both null outside an
    // active reset flow.
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
}
