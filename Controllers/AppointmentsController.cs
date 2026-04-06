using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<AppointmentsHub> _hubContext;
    private readonly IConfiguration _configuration;
    public AppointmentsController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<AppointmentsHub> hubContext, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var appointments = await _context.Appointments.OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment == null)
            return NotFound();
        return Ok(appointment);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentDTO updatedAppointment)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment == null)
            return NotFound();
        var customer = await _context.Customers.FindAsync(updatedAppointment.CustomerId);
        if (customer == null)
            return BadRequest("Invalid Customer");
        var worker = await _context.Workers.FindAsync(updatedAppointment.WorkerId);
        if (worker == null)
            return BadRequest("Invalid Worker");
        var service = await _context.Services.FindAsync(updatedAppointment.ServiceId);
        if (service == null)
            return BadRequest("Invalid Service");

        appointment.Customer = customer;
        appointment.Worker = worker;
        appointment.Service = service;
        appointment.ScheduledFor = updatedAppointment.ScheduledFor;
        appointment.Status = updatedAppointment.Status;
        if (updatedAppointment.Status == Status.Completed && updatedAppointment.CompletedAt == null)
            appointment.CompletedAt = DateTime.UtcNow;
        else
            appointment.CompletedAt = null;
        appointment.ExtraDetails = updatedAppointment.ExtraDetails;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> VirtualDelete(int id)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment == null)
            return NotFound();
        if (appointment.Status == Status.Cancelled)
            return BadRequest("Appointment is already cancelled");
        if (appointment.Status == Status.Completed)
            return BadRequest("Completed appointments cannot be cancelled");
        if (appointment.Status == Status.Deleted)
            return BadRequest("Appointment is already deleted");
        appointment.Status = Status.Deleted;
        appointment.CompletedAt = DateTime.UtcNow;
        _context.Appointments.Update(appointment);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");
        return NoContent();
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentDTO newAppointment)
    {
        var customer = await _context.Customers.FindAsync(newAppointment.CustomerId);
        if (customer == null)
            return BadRequest("Invalid Customer");
        var worker = await _context.Workers.FindAsync(newAppointment.WorkerId);
        if (worker == null)
            return BadRequest("Invalid Worker");
        var service = await _context.Services.FindAsync(newAppointment.ServiceId);
        if (service == null)
            return BadRequest("Invalid Service");

        var workerAvailabilityTask = IsWorkerAvailable(worker.Id, newAppointment.ScheduledFor, service.Duration);
        var customerAvailabilityTask = IsCustomerAvailable(customer.Id, newAppointment.ScheduledFor, service.Duration);

        await Task.WhenAll(workerAvailabilityTask, customerAvailabilityTask);
        var isWorkerAvailable = workerAvailabilityTask.Result;
        var isCustomerAvailable = customerAvailabilityTask.Result;

        if (!isWorkerAvailable)
            return BadRequest("Worker is not available at the selected time");
        if (!isCustomerAvailable)
            return BadRequest("Customer has an appointment already scheduled at the selected time");

        var appointment = new Appointment
        {
            Customer = customer,
            Worker = worker,
            Service = service,
            ScheduledFor = newAppointment.ScheduledFor,
            CompletedAt = newAppointment.CompletedAt,
            Status = newAppointment.Status,
            ExtraDetails = newAppointment.ExtraDetails
        };
        await _context.Appointments.AddAsync(appointment);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");

        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    [HttpGet("{dateStart:datetime, dateEnd:datetime}")]
    public async Task<IActionResult> GetByDateRange(DateTime dateStart, DateTime dateEnd)
    {
        var appointments = await _context.Appointments.Where(a => a.ScheduledFor >= dateStart && a.ScheduledFor <= dateEnd).OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var appointments = await _context.Appointments.Where(a => a.Worker.Id == workerId).OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var appointments = await _context.Appointments.Where(a => a.Customer.Id == customerId).OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
    {
        var appointments = await _context.Appointments.Where(a => a.Service.Id == serviceId).OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
    {
        var appointments = await _context.Appointments.Where(a => a.Status == status).OrderByDescending(a => a.Status).ToListAsync();
        return Ok(appointments);
    }
}
