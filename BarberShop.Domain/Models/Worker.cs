namespace BarberShop.Domain.Models;

public class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Service> ProvidedServices { get; set; } = new List<Service>();
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = null!;
    public decimal WagePerHour { get; set; }
    public string Position { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUpdatedAt { get; set; } = DateTime.Now;
}
