using BarberShop.Domain.Models;

namespace BarberShop.Application.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<List<Appointment>?> GetByDateRange(DateTime? dateStart, DateTime? dateEnd);
    Task<List<Appointment>?> GetByWorker(int workerId);
    Task<List<Appointment>?> GetByCustomer(int customerId);
    Task<List<Appointment>?> GetByService(int serviceId);
    Task<List<Appointment>?> GetByStatus(Status status);
    Task VirtualDelete(Appointment obj);
    Task<List<Appointment>> VirtualDeleteRange(List<Appointment> appointments);
}
