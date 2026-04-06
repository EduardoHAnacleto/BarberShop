using BarberShop.Models;

namespace BarberShop.DTOs;

public class AppointmentDTO
{
    public int Id { get; set; }
    public Worker Worker { get; set; } = null!;
    public int WorkerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int CustomerId { get; set; }
    public Service Service { get; set; } = null!;
    public int ServiceId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public Status Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ExtraDetails { get; set; } = string.Empty;
}
