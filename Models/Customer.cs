namespace BarberShop.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; } = DateTime.MinValue!;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastUpdatedAt { get; set; } = DateTime.Now;

}
