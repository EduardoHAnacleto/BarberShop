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
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<AppointmentsHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IAppointmentService _appointmentService;
    public AppointmentsController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<AppointmentsHub> hubContext, IConfiguration configuration, AppointmentService appointmentService)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _appointmentService = appointmentService;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var appointments = await _context.Appointments.Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (appointment == null)
            return NotFound();
        return Ok(appointment);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentRequestDTO updatedAppointment)
    {
        var appointment = await _context.Appointments
            .AsTracking()
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .FirstOrDefaultAsync(p => p.Id == id);

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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentRequestDTO newAppointment)
    {
        var customer = await _context.Customers.AnyAsync(c => c.Id == newAppointment.CustomerId);
        if (!customer)
            return BadRequest("Invalid Customer");

        var worker = await _context.Workers.AnyAsync(p => p.Id == newAppointment.WorkerId);
        if (!worker)
            return BadRequest("Invalid Worker");

        var service = await _context.Services.FindAsync(newAppointment.ServiceId);
        if (service == null)
            return BadRequest("Invalid Service");

        var workerAvailabilityTask = _appointmentService.IsWorkerAvailable(newAppointment.WorkerId, newAppointment.ScheduledFor, service.Duration);
        var customerAvailabilityTask = _appointmentService.IsCustomerAvailable(newAppointment.CustomerId, newAppointment.ScheduledFor, service.Duration);

        await Task.WhenAll(workerAvailabilityTask, customerAvailabilityTask);
        var isWorkerAvailable = workerAvailabilityTask.Result;
        var isCustomerAvailable = customerAvailabilityTask.Result;

        if (!isWorkerAvailable)
            return BadRequest("Worker is not available at the selected time");
        if (!isCustomerAvailable)
            return BadRequest("Customer has an appointment already scheduled at the selected time");

        var appointment = new Appointment
        {
            CustomerId = newAppointment.CustomerId,
            WorkerId = newAppointment.WorkerId,
            ServiceId = newAppointment.ServiceId,
            ScheduledFor = newAppointment.ScheduledFor,
            CompletedAt = newAppointment.CompletedAt,
            Status = newAppointment.Status,
            ExtraDetails = newAppointment.ExtraDetails
        };
        await _context.Appointments.AddAsync(appointment);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");

        var response = new AppointmentResponseDTO
        {
            Id = appointment.Id,
            CustomerId = appointment.CustomerId,
            CustomerName = (await _context.Customers.FindAsync(appointment.CustomerId))?.Name,
            WorkerId = appointment.WorkerId,
            WorkerName = (await _context.Workers.FindAsync(appointment.WorkerId))?.Name,
            ServiceId = appointment.ServiceId,
            ServiceName = (await _context.Services.FindAsync(appointment.ServiceId))?.Name,
            ScheduledFor = appointment.ScheduledFor,
            CompletedAt = appointment.CompletedAt,
            Status = appointment.Status,
            ExtraDetails = appointment.ExtraDetails,
            CreatedAt = appointment.CreatedAt,
        };

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetByDateRange([FromQuery]DateTime dateStart, [FromQuery]DateTime dateEnd)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.ScheduledFor >= dateStart && a.ScheduledFor <= dateEnd)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Worker.Id == workerId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Customer.Id == customerId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Service.Id == serviceId)
            .OrderByDescending(a => a.ScheduledFor).ToListAsync();
        return Ok(appointments);
    }
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
    {
        var appointments = await _context.Appointments
            .Include(p => p.Customer)
            .Include(p => p.Worker)
            .Include(p => p.Service)
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.Status)
            .ToListAsync();
        return Ok(appointments);
    }
}
