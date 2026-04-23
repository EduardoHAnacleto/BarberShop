using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IWorkerRepository : IRepository<Worker>
{
    Task<List<Service>?> GetServicesByWorker(int id);
    Task<List<Worker>?> GetWorkersByService(string serviceName);
}
