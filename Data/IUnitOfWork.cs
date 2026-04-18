using BarberShop.Repositories.Interfaces;

namespace BarberShop.Data;

public interface IUnitOfWork : IDisposable
{
    IAppointmentRepository Appointments { get; }
    ICustomerRepository Customers { get; }
    IServiceRepository Services { get; }
    IWorkerRepository Workers { get; }
    IUserRepository Users { get; }
    IWorkingHoursRepository WorkingHours { get; }
    IBusinessScheduleRepository BusinessSchedules { get; }

    Task<int> SaveAsync();
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
