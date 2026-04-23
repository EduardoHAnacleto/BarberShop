namespace BarberShop.Domain.Models;

public class Appointment
{
    public int Id { get; set; }
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
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUpdatedAt { get; set; } = DateTime.Now;

}
