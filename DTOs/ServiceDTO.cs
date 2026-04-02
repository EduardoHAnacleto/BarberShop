namespace BarberShop.DTOs;

public class ServiceDTO
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; }
    public decimal Price { get; set; }
}
