using BarberShop.Application.Interfaces;
using BarberShop.Infrastructure.Data;
using BarberShop.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace BarberShop.Infrastructure.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public IAppointmentRepository Appointments { get; }
    public ICustomerRepository Customers { get; }
    public IServiceRepository Services { get; }
    public IWorkerRepository Workers { get; }
    public IUserRepository Users { get; }
    public IWorkingHoursRepository WorkingHours { get; }
    public IBusinessScheduleRepository BusinessSchedules { get; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Appointments = new AppointmentRepository(context);
        Customers = new CustomerRepository(context);
        Services = new ServiceRepository(context);
        Workers = new WorkerRepository(context);
        Users = new UserRepository(context);
        WorkingHours = new WorkingHoursRepository(context);
        BusinessSchedules = new BusinessScheduleRepository(context);
    }

    public async Task<int> SaveAsync()
        => await _context.SaveChangesAsync();

    public async Task BeginTransactionAsync()
        => _transaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitAsync()
    {
        await _context.SaveChangesAsync();
        await _transaction!.CommitAsync();
    }

    public async Task RollbackAsync()
        => await _transaction!.RollbackAsync();

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
