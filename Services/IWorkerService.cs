using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Services;

public interface IWorkerService
{
    Task<Worker> CreateFromDTO(WorkerDTO dto);
}
