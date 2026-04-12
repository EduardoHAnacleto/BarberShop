using BarberShop.Models;

namespace BarberShop.Services;

public interface IWorkingHoursService
{
    Task<List<Appointment>> DelayAppointments(TimeSpan time);
    Task<List<Appointment>> CancelAppointments(DateTime fromTime, DateTime? untilTime);
    Task<bool> IsOpenAsync(DateTime dateTime);
}
