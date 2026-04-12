using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Models;
using BarberShop.Repositories;
using BarberShop.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    private readonly ICustomerRepository _customerRepository;
    private readonly IWorkerRepository _workerRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IMapper _mapper;
    private readonly IWorkingHoursService _workingHoursService;
    public AppointmentService(AppDbContext context, IWorkerRepository workerRepository,
        IServiceRepository serviceRepository, ICustomerRepository customerRepository,
        IMapper mapper, IWorkingHoursService workingHoursService)
    {
        _context = context;
        _workerRepository = workerRepository;
        _serviceRepository = serviceRepository;
        _customerRepository = customerRepository;
        _mapper = mapper;
        _workingHoursService = workingHoursService;
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

    public async Task<Result<Appointment>> CreateFromDTO(AppointmentRequestDTO dto)
    {
        var customerTask = _customerRepository.GetByIdAsync(dto.CustomerId);
        var serviceTask = _serviceRepository.GetByIdAsync(dto.ServiceId);
        var workerTask = _workerRepository.GetByIdAsync(dto.WorkerId);
        await Task.WhenAll(customerTask, serviceTask, workerTask);

        var customer = customerTask.Result;
        var worker = workerTask.Result;
        var service = serviceTask.Result;
        if (customer == null)
            return Result<Appointment>.Fail("Customer not found");
        if (worker == null)
            return Result<Appointment>.Fail("Worker not found");
        if (service == null)
            return Result<Appointment>.Fail("Service not found");

        // Check business hours availability
        var isOpen = await _workingHoursService.IsOpenAsync(dto.ScheduledFor);
        if (!isOpen)
            return Result<Appointment>.Fail("The establishment is closed at the selected time");

        // Check Worker and Customer availability in parallel
        var workerAvailabilityTask = IsWorkerAvailable(
            worker.Id,
            dto.ScheduledFor,
            service.Duration
        );
        var customerAvailabilityTask = IsCustomerAvailable(
            customer.Id,
            dto.ScheduledFor,
            service.Duration
        );
        await Task.WhenAll(workerAvailabilityTask, customerAvailabilityTask);

        var isWorkerAvailable = workerAvailabilityTask.Result;
        var isCustomerAvailable = customerAvailabilityTask.Result;

        if (!isWorkerAvailable)
            return Result<Appointment>.Fail("Worker is not available at the selected time");
        if (!isCustomerAvailable)
            return Result<Appointment>.Fail("Customer has an appointment already scheduled at the selected time");

        var appointment = _mapper.Map<Appointment>(dto);

        return Result<Appointment>.Ok(appointment);
    }
}
