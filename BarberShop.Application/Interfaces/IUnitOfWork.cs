namespace BarberShop.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IAppointmentRepository Appointments { get; }
    ICustomerRepository Customers { get; }
    IServiceRepository Services { get; }
    IWorkerRepository Workers { get; }
    IUserRepository Users { get; }
    IWorkingHoursRepository WorkingHours { get; }
    IBusinessScheduleRepository BusinessSchedules { get; }
    IReviewRepository Reviews { get; }
    IWaitlistRepository Waitlist { get; }
    IWorkerScheduleRepository WorkerSchedules { get; }

    Task<int> SaveAsync();
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
