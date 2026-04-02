namespace BarberShop.Utilities;

public abstract class Utilities
{
    public static TimeZoneInfo GetTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
