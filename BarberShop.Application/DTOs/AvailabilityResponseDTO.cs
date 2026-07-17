namespace BarberShop.Application.DTOs;

public class AvailabilityResponseDTO
{
    public int WorkerId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Requested day, ISO format (yyyy-MM-dd).</summary>
    public string Date { get; set; } = null!;

    /// <summary>Bookable start times ("HH:mm"), already filtered for schedule,
    /// break, closures, existing appointments, service duration fit and the
    /// same-day minimum lead time.</summary>
    public List<string> Slots { get; set; } = [];

    /// <summary>
    /// False when the day is outside the shop's weekly schedule (or already
    /// past) — as opposed to open with every slot taken. Lets the frontend
    /// distinguish "closed" from "fully booked" (only the latter can offer a
    /// waitlist). Does not currently account for a full-day WorkingHours
    /// closure record landing on an otherwise-open weekday; that case still
    /// reports IsOpen=true with zero slots.
    /// </summary>
    public bool IsOpen { get; set; } = true;
}
