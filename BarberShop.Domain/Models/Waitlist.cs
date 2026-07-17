namespace BarberShop.Domain.Models;

// A customer's request to be notified if a slot opens up for a worker+service
// on a given day that was fully booked at request time.
public class Waitlist
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int WorkerId { get; set; }
    public Worker Worker { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    /// <summary>Date only — the customer wants any open slot that day.</summary>
    public DateTime PreferredDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set once a cancellation for this worker+date triggers a notification email.</summary>
    public DateTime? NotifiedAt { get; set; }
}
