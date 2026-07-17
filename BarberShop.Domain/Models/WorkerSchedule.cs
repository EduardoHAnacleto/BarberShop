namespace BarberShop.Domain.Models;

// A worker's own override of the shop's shared BusinessSchedule for one day
// of the week. Absence of a row for a given (WorkerId, DayOfWeek) means the
// worker follows the shop default for that day.
public class WorkerSchedule
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsOpen { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
}
