namespace BarberShop.Application.DTOs;

// Batch cancel payload: the appointment ids to cancel.
public class CancelAppointmentsDTO
{
    public List<int> IdList { get; set; } = [];
}

// Batch delay payload: the appointment ids and the amount of time to push them.
public class DelayAppointmentsDTO
{
    public List<int> IdList { get; set; } = [];
    public TimeSpan TimeSpan { get; set; }
}

// Status transition payload for the worker portal (start / complete / no-show).
public class ChangeStatusDTO
{
    public Domain.Models.Status Status { get; set; }
}

// Payload for booking a weekly-recurring series in one request.
public class RecurringAppointmentRequestDTO
{
    public int WorkerId { get; set; }
    public int CustomerId { get; set; }
    public int ServiceId { get; set; }
    /// <summary>First occurrence; subsequent ones are 7 days apart.</summary>
    public DateTime ScheduledFor { get; set; }
    public string ExtraDetails { get; set; } = string.Empty;
    /// <summary>Total number of occurrences to attempt, including the first (1–12).</summary>
    public int RepeatWeeks { get; set; }
}

// Outcome of a recurring booking: what was actually created vs. skipped
// because that week's slot was already taken.
public class RecurringAppointmentResultDTO
{
    public Guid RecurrenceId { get; set; }
    public List<AppointmentResponseDTO> Created { get; set; } = [];
    public List<DateTime> SkippedDates { get; set; } = [];
}
