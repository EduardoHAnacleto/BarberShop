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
using System.Timers;

namespace BarberShop.Services;

public class AppointmentsService : BaseService, IAppointmentsService
{

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<AppointmentsHub> _hub;

    public AppointmentsService(
        IUnitOfWork uow,
        IMapper mapper,
        RedisService redis,
        IHubContext<AppointmentsHub> hub) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetAllAsync()
    {
        var result = await GetCachedAsync(
            "appointments:all",
            async () => _mapper.Map<List<AppointmentResponseDTO>>(
                await _uow.Appointments.GetAllAsync(
                    null,
                    q => q.OrderByDescending(a => a.ScheduledFor),
                    a => a.Customer,
                    a => a.Worker,
                    a => a.Service))
        );

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<AppointmentResponseDTO?> GetByIdAsync(int id)
    {
        return await GetCachedAsync(
            $"appointments:{id}",
            async () =>
            {
                var entity = await _uow.Appointments.GetByIdAsync(id,
                    a => a.Customer,
                    a => a.Worker,
                    a => a.Service);

                return entity == null ? null : _mapper.Map<AppointmentResponseDTO>(entity);
            }
        );
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Create(AppointmentRequestDTO dto)
    {
        var entity = _mapper.Map<Appointment>(dto);

        await _uow.Appointments.AddAsync(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        return Result<AppointmentResponseDTO>.Ok(_mapper.Map<AppointmentResponseDTO>(entity));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Update(int id, AppointmentRequestDTO dto)
    {
        var entity = await _uow.Appointments.GetByIdAsync(id);

        if (entity == null)
            return Result<AppointmentResponseDTO>.Ok(null);

        _mapper.Map(dto, entity);

        if (dto.Status == Status.Completed && entity.CompletedAt == null)
            entity.CompletedAt = DateTime.UtcNow;

        _uow.Appointments.Update(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        return Result<AppointmentResponseDTO>.Ok(_mapper.Map<AppointmentResponseDTO>(entity));
    }

    // =========================
    // DELETE (Virtual)
    // =========================
    public async Task<Result<AppointmentResponseDTO>> Delete(int id)
    {
        var entity = await _uow.Appointments.GetByIdAsync(id);

        if (entity == null)
            return Result<AppointmentResponseDTO>.Fail("Appointment not found");

        if (entity.Status == Status.Cancelled)
            return Result<AppointmentResponseDTO>.Fail("Already cancelled");

        if (entity.Status == Status.Completed)
            return Result<AppointmentResponseDTO>.Fail("Completed cannot be cancelled");

        if (entity.Status == Status.Deleted)
            return Result<AppointmentResponseDTO>.Fail("Already deleted");

        await _uow.Appointments.VirtualDelete(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("appointments", _hub, "AppointmentsChanged");

        return Result<AppointmentResponseDTO>.Ok(null);
    }

    // =========================
    // FILTERS
    // =========================
    public async Task<List<AppointmentResponseDTO>> GetByDateRange(DateTime start, DateTime end)
    {
        var data = await _uow.Appointments.GetAllAsync(
            a => a.ScheduledFor >= start && a.ScheduledFor <= end,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByWorker(int workerId)
    {
        var data = await _uow.Appointments.GetAllAsync(
            a => a.WorkerId == workerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByCustomer(int customerId)
    {
        var data = await _uow.Appointments.GetAllAsync(
            a => a.CustomerId == customerId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByService(int serviceId)
    {
        var data = await _uow.Appointments.GetAllAsync(
            a => a.ServiceId == serviceId,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    public async Task<List<AppointmentResponseDTO>> GetByStatus(Status status)
    {
        var data = await _uow.Appointments.GetAllAsync(
            a => a.Status == status,
            q => q.OrderByDescending(a => a.ScheduledFor),
            a => a.Customer, a => a.Worker, a => a.Service);

        return _mapper.Map<List<AppointmentResponseDTO>>(data);
    }

    // =========================

    public async Task<List<AppointmentResponseDTO>> DelayAppointments(List<int> idList, TimeSpan time)
    {
        var tasks = idList.Select(async id =>
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity != null)
            {
                entity.ScheduledFor = entity.ScheduledFor.Add(time);
                entity.LastUpdatedAt = DateTime.UtcNow;
                _uow.Appointments.Update(entity);
                return _mapper.Map<AppointmentResponseDTO>(entity);
            }

            return null;
        });

        var results = await Task.WhenAll(tasks);

        return results.Where(x => x != null).ToList();
    }

    public async Task<List<AppointmentResponseDTO>> CancelAppointments(List<int> idList)
    {
        var tasks = idList.Select(async id =>
        {
            var entity = await _uow.Appointments.GetByIdAsync(id);

            if (entity != null)
            {
                await _uow.Appointments.VirtualDelete(entity);
                return _mapper.Map<AppointmentResponseDTO>(entity);
            }

            return null;
        });

        var results = await Task.WhenAll(tasks);

        return results.Where(x => x != null).ToList();
    }
}