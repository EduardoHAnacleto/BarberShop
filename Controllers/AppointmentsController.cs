using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;
    public AppointmentsController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<WorkersHub> hubContext, IConfiguration configuration)
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
        var appointments = _context.Appointments.ToList();
        return Ok(appointments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = _context.Appointments.Find(id);
        if (appointment == null)
            return NotFound();
        return Ok(appointment);
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentDTO updatedAppointment)
    {
        var appointment = _context.Appointments.Find(id);
        if (appointment == null)
            return NotFound();
        var customer = _context.Customers.Find(updatedAppointment.CustomerId);
        if (customer == null)
            return BadRequest("Invalid Customer");
        var worker = _context.Workers.Find(updatedAppointment.WorkerId);
        if (worker == null)
            return BadRequest("Invalid Worker");
        var service = _context.Services.Find(updatedAppointment.ServiceId);
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

        _context.SaveChanges();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> VirtualDelete(int id)
    {
        var appointment = _context.Appointments.Find(id);
        if (appointment == null)
            return NotFound();
        appointment.Status = Status.Cancelled;
        appointment.CompletedAt = DateTime.UtcNow;
        _context.Appointments.Update(appointment);
        _context.SaveChanges();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentDTO newAppointment)
    {
        var customer = _context.Customers.Find(newAppointment.CustomerId);
        if (customer == null)
            return BadRequest("Invalid Customer");
        var worker = _context.Workers.Find(newAppointment.WorkerId);
        if (worker == null)
            return BadRequest("Invalid Worker");
        var service = _context.Services.Find(newAppointment.ServiceId);
        if (service == null)
            return BadRequest("Invalid Service");
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
        _context.Appointments.Add(appointment);
        _context.SaveChanges();
        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    [HttpGet("{dateStart:datetime, dateEnd:datetime}")]
    public async Task<IActionResult> GetByDateRange(DateTime dateStart, DateTime dateEnd)
    {
        var appointments = _context.Appointments.Where(a => a.ScheduledFor >= dateStart && a.ScheduledFor <= dateEnd).ToList();
        return Ok(appointments);
    }

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var appointments = _context.Appointments.Where(a => a.Worker.Id == workerId).ToList();
        return Ok(appointments);
    }
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var appointments = _context.Appointments.Where(a => a.Customer.Id == customerId).ToList();
        return Ok(appointments);
    }
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
    {
        var appointments = _context.Appointments.Where(a => a.Service.Id == serviceId).ToList();
        return Ok(appointments);
    }
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
    {
        var appointments = _context.Appointments.Where(a => a.Status == status).ToList();
        return Ok(appointments);
    }
}
