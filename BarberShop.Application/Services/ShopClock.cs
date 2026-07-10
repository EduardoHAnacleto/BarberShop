using BarberShop.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BarberShop.Application.Services;

public class ShopClock : IShopClock
{
    private readonly TimeZoneInfo _zone;

    public ShopClock(IConfiguration config)
    {
        // Shop:TimeZone accepts an IANA id (e.g. "Pacific/Auckland") or a
        // Windows id. When unset, the host's local zone is used — correct for
        // single-machine dev, and containers should set it explicitly.
        var id = config["Shop:TimeZone"];

        _zone = string.IsNullOrWhiteSpace(id)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);
}
