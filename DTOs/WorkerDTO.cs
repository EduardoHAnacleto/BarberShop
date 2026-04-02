using BarberShop.Models;

namespace BarberShop.DTOs;

public class WorkerDTO
{
    public string Name { get; set; } = null!;
    public ICollection<Service> ProvidedServices { get; set; } = new List<Service>();
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal WagePerHour { get; set; }
    public string Position { get; set; }
}
