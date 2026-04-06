namespace BarberShop.Models;

public class WorkerService
{
    public int ServiceId { get; set; }
    public Service Service { get; set; }
    public int WorkerId { get; set; }
    public Worker Worker { get; set; }
}
