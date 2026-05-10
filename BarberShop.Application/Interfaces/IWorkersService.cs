using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IWorkersService
{
    Task<List<WorkerDTO>> GetAllAsync();
    Task<PagedResult<WorkerDTO>> GetAllAsync(PaginationParams pagination);
    Task<WorkerDTO?> GetByIdAsync(int id);
    Task<Result<WorkerDTO>> Create(WorkerDTO dto);
    Task<Result<WorkerDTO>> Update(int id, WorkerDTO dto);
    Task<Result<WorkerDTO>> Delete(int id);
    Task<List<ServiceDTO>?> GetServicesByWorker(int id);
    Task<List<WorkerDTO>> GetWorkersByService(int id);
}
