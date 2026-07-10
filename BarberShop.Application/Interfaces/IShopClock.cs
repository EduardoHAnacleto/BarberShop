namespace BarberShop.Application.Interfaces;

/// <summary>
/// Source of "now" in the shop's wall-clock time zone. Appointment datetimes
/// are stored as naive local (shop) times, so every past/future comparison
/// must go through this abstraction instead of DateTime.UtcNow — the API
/// container's clock (UTC) is not the shop's clock.
/// </summary>
public interface IShopClock
{
    DateTime Now { get; }
}
