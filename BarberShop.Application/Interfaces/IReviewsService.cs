using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IReviewsService
{
    Task<Result<ReviewResponseDTO>> Create(int customerId, ReviewRequestDTO dto);
    Task<List<ReviewResponseDTO>> GetByWorkerAsync(int workerId);
    Task<List<WorkerRatingSummaryDTO>> GetSummaryAsync();
    Task<List<ReviewResponseDTO>> GetMineAsync(int customerId);
    Task<List<ReviewResponseDTO>> GetAllAsync();
    Task<Result<ReviewResponseDTO>> Delete(int id);
}
