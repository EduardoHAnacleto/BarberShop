using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;

namespace BarberShop.Application.Services;

public class ReportsService : IReportsService
{
    private const int TopListSize = 5;

    private readonly IUnitOfWork _uow;
    private readonly IShopClock _clock;

    public ReportsService(IUnitOfWork uow, IShopClock clock)
    {
        _uow = uow;
        _clock = clock;
    }

    public async Task<ReportsSummaryDTO> GetSummaryAsync()
    {
        var appointments = await _uow.Appointments.GetAllAsync(
            includes: [a => a.Service, a => a.Worker]);

        var completed = appointments.Where(a => a.Status == Status.Completed).ToList();
        var cancelledCount = appointments.Count(a => a.Status == Status.Cancelled);

        var cutoff = _clock.Now.AddDays(-30);
        var denominator = completed.Count + cancelledCount;

        return new ReportsSummaryDTO
        {
            TotalRevenue = completed.Sum(a => a.Service.Price),
            RevenueLast30Days = completed.Where(a => a.ScheduledFor >= cutoff).Sum(a => a.Service.Price),
            CompletedCount = completed.Count,
            CancelledCount = cancelledCount,
            CancellationRate = denominator == 0 ? 0 : (double)cancelledCount / denominator,
            TopServicesByRevenue = completed
                .GroupBy(a => new { a.ServiceId, a.Service.Name })
                .Select(g => new ServiceRevenueDTO
                {
                    ServiceId = g.Key.ServiceId,
                    ServiceName = g.Key.Name,
                    Revenue = g.Sum(a => a.Service.Price),
                    CompletedCount = g.Count(),
                })
                .OrderByDescending(s => s.Revenue)
                .Take(TopListSize)
                .ToList(),
            TopWorkersByRevenue = completed
                .GroupBy(a => new { a.WorkerId, a.Worker.Name })
                .Select(g => new WorkerRevenueDTO
                {
                    WorkerId = g.Key.WorkerId,
                    WorkerName = g.Key.Name,
                    Revenue = g.Sum(a => a.Service.Price),
                    CompletedCount = g.Count(),
                })
                .OrderByDescending(w => w.Revenue)
                .Take(TopListSize)
                .ToList(),
        };
    }
}
