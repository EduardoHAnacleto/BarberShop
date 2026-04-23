using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Application.Services;

public abstract class BaseService
{
    protected readonly IRedisService _redis;

    protected BaseService(IRedisService redis)
    {
        _redis = redis;
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

    protected async Task InvalidateAndNotifyAsync<THub>(
        string cachePrefix,
        IHubContext<THub> hub,
        string eventName)
        where THub : Hub
    {
        await _redis.InvalidateByPrefixAsync(cachePrefix);
        await hub.Clients.All.SendAsync(eventName);
    }
}