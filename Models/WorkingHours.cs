namespace BarberShop.Models;

public class WorkingHours
{
    public int Id { get; set; }
    public DateTime ClosedFrom { get; set; }
    public DateTime? ClosedUntil { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClosureType ClosureType { get; set; }
}
