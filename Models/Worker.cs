namespace BarberShop.Models;

public class Worker
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ICollection<Service> ProvidedServices { get; set; }
    public int PhoneNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; }
}
