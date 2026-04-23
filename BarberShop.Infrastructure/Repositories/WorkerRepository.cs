using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Infrastructure.Repositories;

public class WorkerRepository : GenericRepository<Worker> ,IWorkerRepository
{
    public WorkerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Service>?> GetServicesByWorker(int id)
    {
        var worker = await _context.Workers
            .Include(w => w.ProvidedServices)
            .FirstOrDefaultAsync(w => w.Id == id);

        return worker?.ProvidedServices.ToList();
    }

    public async Task<List<Worker>?> GetWorkersByService(string serviceName)
    {
        var list = await _context.Workers.Where(s => s.ProvidedServices.Any(p => p.Name == serviceName)).ToListAsync();

        return list;
    }
}
