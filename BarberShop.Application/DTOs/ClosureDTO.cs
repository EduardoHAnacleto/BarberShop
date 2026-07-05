using BarberShop.Domain.Enums;

namespace BarberShop.Application.DTOs;

public class ClosureDTO
{
    public DateTime ClosedFrom { get; set; }
    public DateTime? ClosedUntil { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClosureType ClosureType { get; set; }
}
