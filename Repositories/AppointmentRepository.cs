using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Repositories;

public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<Appointment>?> GetByDateRange(DateTime? dateStart, DateTime? dateEnd)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.ScheduledFor >= dateStart && a.ScheduledFor <= dateEnd)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return appointments;
    }

    public async Task<List<Appointment>?> GetByWorker(int workerId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Worker.Id == workerId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return appointments;
    }

    public async Task<List<Appointment>?> GetByCustomer(int customerId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Customer.Id == customerId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return appointments;
    }

    public async Task<List<Appointment>?> GetByService(int serviceId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Service.Id == serviceId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return appointments;
    }

    public async Task<List<Appointment>?> GetByStatus(Status status)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.Status)
            .ToListAsync();
        return appointments;
    }

    public async Task VirtualDelete(Appointment obj)
    {
        obj.Status = Status.Deleted;
        obj.CompletedAt = DateTime.UtcNow;
        _context.Appointments.Update(obj);
        return;
    }

    public async Task<List<Appointment>> VirtualDeleteRange(List<Appointment> appointments)
    {
        foreach (var appointment in appointments)
        {
            appointment.Status = Status.Cancelled;
            appointment.CompletedAt = DateTime.UtcNow;
        }
        _context.UpdateRange(appointments);
        return appointments;
    }
}
