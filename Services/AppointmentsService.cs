using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Services;

public class AppointmentsService : IAppointmentsService
{
    private readonly IAppointmentRepository _repository;
    private readonly IMapper _mapper;
    private readonly AppDbContext _context;
    private readonly RedisService _redis;
    private readonly IHubContext<AppointmentsHub> _hub;

    public AppointmentsService(
        IAppointmentRepository repository,
        IMapper mapper,
        AppDbContext context,
        RedisService redis,
        IHubContext<AppointmentsHub> hub)
    {
        _repository = repository;
        _mapper = mapper;
        _context = context;
        _redis = redis;
        _hub = hub;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetAllAsync()
    {
        var cacheKey = "appointments:all";

        var cached = await _redis.GetAsync<List<AppointmentResponseDTO>>(cacheKey);
        if (cached != null)
            return cached;

        var data = await _repository.GetAllAsync(
            null,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service
        );

        var dto = _mapper.Map<List<AppointmentResponseDTO>>(data);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }

    public async Task<AppointmentResponseDTO?> GetByIdAsync(int id)
    {
        var cacheKey = $"appointments:{id}";

        var cached = await _redis.GetAsync<AppointmentResponseDTO>(cacheKey);
        if (cached != null)
            return cached;

        var entity = await _repository.GetByIdAsync(id,
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        if (entity == null)
            return null;

        var dto = _mapper.Map<AppointmentResponseDTO>(entity);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Create(AppointmentRequestDTO dto)
    {
        var entity = _mapper.Map<Appointment>(dto);

        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("appointments");
        await _hub.Clients.All.SendAsync("AppointmentsChanged");

        var response = _mapper.Map<AppointmentResponseDTO>(entity);

        return Result<AppointmentResponseDTO>.Ok(response);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Update(int id, AppointmentRequestDTO dto)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            return Result<AppointmentResponseDTO>.Ok(null);

        _mapper.Map(dto, entity);

        if (dto.Status == Status.Completed && entity.CompletedAt == null)
            entity.CompletedAt = DateTime.UtcNow;

        _repository.Update(entity);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("appointments");
        await _hub.Clients.All.SendAsync("AppointmentsChanged");

        var response = _mapper.Map<AppointmentResponseDTO>(entity);

        return Result<AppointmentResponseDTO>.Ok(response);
    }

    // =========================
    // DELETE (Virtual)
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Delete(int id)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            return Result<AppointmentResponseDTO>.Ok(null);

        if (entity.Status == Status.Cancelled)
            return Result<AppointmentResponseDTO>.Fail("Already cancelled");

        if (entity.Status == Status.Completed)
            return Result<AppointmentResponseDTO>.Fail("Completed cannot be cancelled");

        if (entity.Status == Status.Deleted)
            return Result<AppointmentResponseDTO>.Fail("Already deleted");

        await _repository.VirtualDelete(entity);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("appointments");
        await _hub.Clients.All.SendAsync("AppointmentsChanged");

        return Result<AppointmentResponseDTO>.Ok(null);
    }

    // =========================
    // FILTERS
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetByDateRange(DateTime start, DateTime end)
    {
        var data = await _repository.GetAllAsync(
            a => a.ScheduledFor >= start && a.ScheduledFor <= end,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByWorker(int workerId)
    {
        var data = await _repository.GetAllAsync(
            a => a.WorkerId == workerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByCustomer(int customerId)
    {
        var data = await _repository.GetAllAsync(
            a => a.CustomerId == customerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByService(int serviceId)
    {
        var data = await _repository.GetAllAsync(
            a => a.ServiceId == serviceId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByStatus(Status status)
    {
        var data = await _repository.GetAllAsync(
            a => a.Status == status,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer,
            a => a.Worker,
            a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }
}