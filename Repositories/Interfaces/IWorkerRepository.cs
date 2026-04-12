using BarberShop.DTOs;
using BarberShop.Models;

namespace BarberShop.Repositories.Interfaces;

public interface IWorkerRepository : IRepository<Worker>
{
    Task<List<Service>?> GetServicesByWorker(int id);
    Task<List<Worker>?> GetWorkersByService(string serviceName);
}
