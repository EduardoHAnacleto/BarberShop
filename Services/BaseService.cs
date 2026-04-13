using BarberShop.Data;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Services;

public abstract class BaseService
{
    protected readonly AppDbContext _context;
    protected readonly RedisService _redis;

    protected BaseService(AppDbContext context, RedisService redis)
    {
        _context = context;
        _redis = redis;
    }

    protected async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
    }

    protected async Task<T?> GetCachedAsync<T>( string cacheKey, Func<Task<T?>> factory, TimeSpan? expiry = null)
    {
        var cached = await _redis.GetAsync<T>(cacheKey);
        if (cached != null)
            return cached;

        var data = await factory();

        if (data != null)
            await _redis.SetAsync(cacheKey, data, expiry ?? TimeSpan.FromMinutes(10));

        return data;
    }

    protected async Task InvalidateAndNotifyAsync<THub>( string cachePrefix, IHubContext<THub> hub, string eventName) where THub : Hub
    {
        await _redis.InvalidateByPrefixAsync(cachePrefix);
        await hub.Clients.All.SendAsync(eventName);
    }
}