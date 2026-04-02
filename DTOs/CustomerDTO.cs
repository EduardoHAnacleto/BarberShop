namespace BarberShop.DTOs;

public class CustomerDTO
{
    public string Name { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; } = DateTime.MinValue!;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
