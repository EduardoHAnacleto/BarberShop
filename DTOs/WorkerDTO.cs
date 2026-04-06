using BarberShop.Models;

namespace BarberShop.DTOs;

public class WorkerDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public List<int> ServicesId { get; set; } = new List<int>();
    public List<ServiceDTO> ProvidedServices { get; set; } = new ();
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal WagePerHour { get; set; }
    public string Position { get; set; }
}
