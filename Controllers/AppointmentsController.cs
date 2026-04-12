using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
    private readonly IMapper _mapper;
    private readonly IAppointmentRepository _repository;
    public AppointmentsController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<AppointmentsHub> hubContext, IConfiguration configuration, IAppointmentService appointmentService, IMapper mapper,
        IAppointmentRepository repository)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _appointmentService = appointmentService;
        _mapper = mapper;
        _repository = repository;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var appointments = await _repository.GetAllAsync(
            orderBy: q => q.OrderByDescending(a => a.ScheduledFor),
            includes: new Expression<Func<Appointment, object>>[]
            {
            a => a.Customer,
            a => a.Worker,
            a => a.Service
            }
        );

        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);

        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _repository.GetByIdAsync(id,
            includes: new Expression<Func<Appointment, object>>[]
            {
            a => a.Customer,
            a => a.Worker,
            a => a.Service
            }
        );
        if (appointment == null)
            return NotFound();
        var dto = _mapper.Map<CustomerDTO>(appointment);
        return Ok(dto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AppointmentRequestDTO updatedAppointment)
    {
        var appointment = await _repository.GetByIdAsync(id,
            includes: new Expression<Func<Appointment, object>>[]
            {
            a => a.Customer,
            a => a.Worker,
            a => a.Service
            }
        );

        if (appointment == null)
            return NotFound();

        var dto = _mapper.Map<AppointmentRequestDTO>(appointment);
        if (updatedAppointment.Status == Status.Completed && updatedAppointment.CompletedAt == null)
            appointment.CompletedAt = DateTime.UtcNow;
        else
            appointment.CompletedAt = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> VirtualDelete(int id)
    {
        var appointment = await _repository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound();
        if (appointment.Status == Status.Cancelled)
            return BadRequest("Appointment is already cancelled");
        if (appointment.Status == Status.Completed)
            return BadRequest("Completed appointments cannot be cancelled");
        if (appointment.Status == Status.Deleted)
            return BadRequest("Appointment is already deleted");

        await _repository.VirtualDelete(appointment);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppointmentRequestDTO newAppointment)
    {
        var obj = await _repository.GetByIdAsync(newAppointment.CustomerId);
        if (obj.Customer == null)
            return BadRequest("Invalid Customer");
        if (obj.Worker == null)
            return BadRequest("Invalid Worker");
        if (obj.Service == null)
            return BadRequest("Invalid Service");

        var workerAvailabilityTask = _appointmentService.IsWorkerAvailable(newAppointment.WorkerId, newAppointment.ScheduledFor, obj.Service.Duration);
        var customerAvailabilityTask = _appointmentService.IsCustomerAvailable(newAppointment.CustomerId, newAppointment.ScheduledFor, obj.Service.Duration);

        await Task.WhenAll(workerAvailabilityTask, customerAvailabilityTask);
        var isWorkerAvailable = workerAvailabilityTask.Result;
        var isCustomerAvailable = customerAvailabilityTask.Result;

        if (!isWorkerAvailable)
            return BadRequest("Worker is not available at the selected time");
        if (!isCustomerAvailable)
            return BadRequest("Customer has an appointment already scheduled at the selected time");

        var appointment = _mapper.Map<Appointment>(newAppointment);
        await _repository.AddAsync(appointment);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("AppointmentsChanged");

        var response = _mapper.Map<AppointmentResponseDTO>(appointment);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetByDateRange([FromQuery]DateTime dateStart, [FromQuery]DateTime dateEnd)
    {
        var appointments = await _repository.GetAllAsync(
            a => a.ScheduledFor >= dateStart && a.ScheduledFor <= dateEnd,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);
        
        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);
        return Ok(dtoList);
    }

    [HttpGet("worker/{workerId:int}")]
    public async Task<IActionResult> GetByWorker(int workerId)
    {
        var appointments = await _repository.GetAllAsync(
            a => a.WorkerId == workerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);
        return Ok(dtoList);
    }
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        var appointments = await _repository.GetAllAsync(
            a => a.CustomerId == customerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);
        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);
        return Ok(dtoList);
    }
    [HttpGet("service/{serviceId:int}")]
    public async Task<IActionResult> GetByService(int serviceId)
    {
        var appointments = await _repository.GetAllAsync(
            a => a.ServiceId == serviceId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);
        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);
        return Ok(dtoList);
    }
    [HttpGet("status/{status}")]
    public async Task<IActionResult> GetByStatus(Status status)
    {
        var appointments = await _repository.GetAllAsync(
            a => a.Status == status,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);
        var dtoList = _mapper.Map<List<AppointmentResponseDTO>>(appointments);
        return Ok(dtoList);
    }
}
