namespace BarberShop.Services;

public interface IAppointmentService
{
    Task<bool> IsWorkerAvailable(int workerId, DateTime scheduledFor, int duration);
    Task<bool> IsCustomerAvailable(int customerId, DateTime scheduledFor, int duration);
}
