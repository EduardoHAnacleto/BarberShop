namespace BarberShop.Application.DTOs;

// Submitted by the client after a completed appointment.
public class ReviewRequestDTO
{
    public int AppointmentId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

// Returned to callers — denormalizes the names a list view needs so the
// front end never has to issue follow-up requests per review.
public class ReviewResponseDTO
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int WorkerId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Aggregate rating for one worker — used to render star badges without
// downloading every individual review.
public class WorkerRatingSummaryDTO
{
    public int WorkerId { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
