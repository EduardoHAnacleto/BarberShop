using BarberShop.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace BarberShop.Application.Services;

public class SecurityStampService : ISecurityStampService
{
    // Short TTL keeps the per-request check on Redis instead of SQL Server
    // while capping how long a stale cached stamp can outlive a rotation
    // (relevant only if the explicit cache invalidation is missed).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IUnitOfWork _uow;
    private readonly IRedisService _redis;
    private readonly ILogger<SecurityStampService> _logger;

    public SecurityStampService(
        IUnitOfWork uow,
        IRedisService redis,
        ILogger<SecurityStampService> logger)
    {
        _uow = uow;
        _redis = redis;
        _logger = logger;
    }

    private static string CacheKey(int userId) => $"users:stamp:{userId}";

    public async Task<bool> ValidateAsync(int userId, string? stamp)
    {
        // Tokens issued before stamp support (or tampered ones) carry no
        // stamp claim — always reject so they cannot outlive a rotation.
        if (string.IsNullOrEmpty(stamp))
            return false;

        var current = await _redis.GetAsync<string>(CacheKey(userId));

        if (current == null)
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return false;

            current = user.SecurityStamp;
            await _redis.SetAsync(CacheKey(userId), current, CacheTtl);
        }

        var valid = string.Equals(current, stamp, StringComparison.Ordinal);

        if (!valid)
            _logger.LogInformation(
                "Rejected revoked token for user {UserId} (stamp mismatch)", userId);

        return valid;
    }

    public Task InvalidateCacheAsync(int userId)
        => _redis.RemoveAsync(CacheKey(userId));
}
