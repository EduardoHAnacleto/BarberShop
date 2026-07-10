using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IReportsService
{
    Task<ReportsSummaryDTO> GetSummaryAsync();
}
