namespace BarberShop.Models;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; } 
    public decimal Price { get; set; } 
}
