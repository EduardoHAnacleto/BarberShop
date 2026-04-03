using BarberShop.Models;

namespace BarberShop.DTOs;

public class AppointmentDTO
{
    public Worker Worker { get; set; }
    public int WorkerId { get; set; }
    public Customer Customer { get; set; }
    public int CustomerId { get; set; }
    public Service Service { get; set; }
    public int ServiceId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public Status Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ExtraDetails { get; set; } = string.Empty;
}
