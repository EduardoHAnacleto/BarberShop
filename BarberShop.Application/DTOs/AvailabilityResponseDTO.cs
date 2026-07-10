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
}
