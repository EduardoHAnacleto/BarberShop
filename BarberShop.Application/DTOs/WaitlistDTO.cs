namespace BarberShop.Application.DTOs;

// Submitted by the client to join the waitlist for a fully-booked day.
public class WaitlistRequestDTO
{
    public int WorkerId { get; set; }
    public int ServiceId { get; set; }
    /// <summary>Date only — any open slot that day satisfies the request.</summary>
    public DateTime PreferredDate { get; set; }
}

public class WaitlistResponseDTO
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int WorkerId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime PreferredDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Notified { get; set; }
}
