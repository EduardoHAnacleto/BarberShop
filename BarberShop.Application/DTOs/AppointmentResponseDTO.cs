using BarberShop.Domain.Models;

namespace BarberShop.Application.DTOs;

public class AppointmentResponseDTO
{
    public int Id { get; set; }
    public string WorkerName { get; set; } = null!;
    public int WorkerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int CustomerId { get; set; }
    public string ServiceName { get; set; } = null!;
    public int ServiceId { get; set; }
    public DateTime ScheduledFor { get; set; }
    public Status Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ExtraDetails { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
