using BarberShop.Data;
using BarberShop.Models;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    public AppointmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsWorkerAvailable(int workerId, DateTime scheduledFor, int appointmentDuration)
    {
        var lastAppointment = await _context.Appointments.Where(a => a.Worker.Id == workerId && a.ScheduledFor.Date == scheduledFor.Date && a.ScheduledFor <= scheduledFor)
            .OrderByDescending(a => a.ScheduledFor)
            .FirstOrDefaultAsync();
        var nextAppointment = await _context.Appointments.Where(a => a.Worker.Id == workerId && a.ScheduledFor.Date == scheduledFor.Date && a.ScheduledFor >= scheduledFor)
            .OrderBy(a => a.ScheduledFor)
            .FirstOrDefaultAsync();
        if (lastAppointment != null &&
            (lastAppointment.Status != Status.Cancelled && lastAppointment.Status != Status.Completed))
        {
            if (lastAppointment.ScheduledFor.AddMinutes(lastAppointment.Service.Duration) > scheduledFor)
            {
                return false;
            }
        }

        if (nextAppointment != null &&
            (nextAppointment.Status != Status.Cancelled && nextAppointment.Status != Status.Completed))
        {
            if (scheduledFor.AddMinutes(appointmentDuration) >= nextAppointment.ScheduledFor)
            {
                return false;
            }
        }
        return true;
    }

    public async Task<bool> IsCustomerAvailable(int id, DateTime scheduledFor, int duration)
    {
        var lastAppointment = await _context.Appointments.Where(a => a.Customer.Id == id && a.ScheduledFor.Date == scheduledFor.Date && a.ScheduledFor <= scheduledFor)
            .OrderByDescending(a => a.ScheduledFor)
            .FirstOrDefaultAsync();
        var nextAppointment = await _context.Appointments.Where(a => a.Customer.Id == id && a.ScheduledFor.Date == scheduledFor.Date && a.ScheduledFor >= scheduledFor)
            .OrderBy(a => a.ScheduledFor)
            .FirstOrDefaultAsync();
        if (lastAppointment != null &&
            (lastAppointment.Status != Status.Cancelled && lastAppointment.Status != Status.Completed))
        {
            if (lastAppointment.ScheduledFor.AddMinutes(lastAppointment.Service.Duration) > scheduledFor)
            {
                return false;
            }
        }

        if (nextAppointment != null &&
            (nextAppointment.Status != Status.Cancelled && nextAppointment.Status != Status.Completed))
        {
            if (scheduledFor.AddMinutes(duration) >= nextAppointment.ScheduledFor)
            {
                return false;
            }
        }
        return true;
    }
}
