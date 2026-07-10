namespace BarberShop.Application.Interfaces;

public interface ISecurityStampService
{
    /// <summary>
    /// True when <paramref name="stamp"/> matches the user's current
    /// SecurityStamp. Reads through a short-lived Redis cache so the check
    /// stays cheap on every authenticated request.
    /// </summary>
    Task<bool> ValidateAsync(int userId, string? stamp);

    /// <summary>
    /// Drops the cached stamp for a user so the next validation re-reads the
    /// database. Call after rotating the stamp (logout / credential change)
    /// to make revocation take effect immediately.
    /// </summary>
    Task InvalidateCacheAsync(int userId);
}
