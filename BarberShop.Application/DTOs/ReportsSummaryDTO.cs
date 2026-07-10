namespace BarberShop.Application.DTOs;

// Revenue and volume rollup for the admin dashboard's analytics panel.
public class ReportsSummaryDTO
{
    public decimal TotalRevenue { get; set; }
    public decimal RevenueLast30Days { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    /// <summary>Cancelled / (Completed + Cancelled), 0 when there is no data yet.</summary>
    public double CancellationRate { get; set; }
    public List<ServiceRevenueDTO> TopServicesByRevenue { get; set; } = [];
    public List<WorkerRevenueDTO> TopWorkersByRevenue { get; set; } = [];
}

public class ServiceRevenueDTO
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int CompletedCount { get; set; }
}

public class WorkerRevenueDTO
{
    public int WorkerId { get; set; }
    public string WorkerName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int CompletedCount { get; set; }
}
