using BarberShop.Application.Interfaces;

namespace BarberShop.Application.Services;

public abstract class BaseService
{
    protected readonly IRedisService _redis;
    protected readonly INotificationPublisher _notifications;

    protected BaseService(IRedisService redis, INotificationPublisher notifications)
    {
        _redis = redis;
        _notifications = notifications;
    }

    protected async Task<T?> GetCachedAsync<T>(
        string cacheKey,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null)
    {
        var cached = await _redis.GetAsync<T>(cacheKey);
        if (cached != null)
            return cached;

        var data = await factory();

        if (data != null)
            await _redis.SetAsync(cacheKey, data, expiry ?? TimeSpan.FromMinutes(10));

        return data;
    }

    protected async Task InvalidateAndNotifyAsync(string cachePrefix, string eventName)
    {
        await _redis.InvalidateByPrefixAsync(cachePrefix);
        await _notifications.PublishAsync(cachePrefix, eventName);
    }
}
