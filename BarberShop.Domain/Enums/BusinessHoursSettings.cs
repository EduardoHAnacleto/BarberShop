namespace BarberShop.Domain.Enums;

public class BusinessHoursSettings 
{
    public Dictionary<DayOfWeek, DaySchedule> Days { get; set; } = new();
}

public class DaySchedule 
{
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
}

public enum ClosureType
{
    UntilNextOpening = 0,
    UntilSpecificDate = 1
}
