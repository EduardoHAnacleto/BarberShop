using StackExchange.Redis;
using System.Text.Json;

namespace BarberShop.Services;

public class RedisService 
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _redis;

    public RedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    // ===============================
    // SET GENERIC
    // ===============================
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, (Expiration)expiry);
    }

    // ===============================
    // GET GENERIC
    // ===============================
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString()!);
    }

    // ===============================
    // REMOVE
    // ===============================
    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }
    // ===============================
    // INVALIDATE BY PREFIX
    // ===============================
    public async Task InvalidateByPrefixAsync(string prefix)
    {
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints.First());

        var keys = server.Keys(
            pattern: $"{prefix}*"
        );

        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }
    }
}