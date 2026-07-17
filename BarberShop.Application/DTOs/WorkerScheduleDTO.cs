namespace BarberShop.Application.DTOs;

public class WorkerScheduleDTO
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsOpen { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
}
