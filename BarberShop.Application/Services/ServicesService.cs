using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Application.Services;

public class ServicesService : BaseService, IServicesService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<ServicesHub> _hub; 

    public ServicesService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<ServicesHub> hub) : base(redis)
    {
        _uow = uow;
        _mapper = mapper;
        _hub = hub;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<ServiceDTO>> GetAllAsync()
    {
        var result = await GetCachedAsync(
            "services:all",
            async () => _mapper.Map<List<ServiceDTO>>(await _uow.Services.GetAllAsync())
        );

        return result ?? [];
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<ServiceDTO?> GetByIdAsync(int id)
    {
        return await GetCachedAsync(
            $"services:{id}",
            async () =>
            {
                var service = await _uow.Services.GetByIdAsync(id);
                return service == null ? null : _mapper.Map<ServiceDTO>(service);
            }
        );
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<ServiceDTO>> Create(ServiceDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length < 3)
            return Result<ServiceDTO>.Fail("Invalid Name");

        if (dto.Duration <= 0)
            return Result<ServiceDTO>.Fail("Invalid Duration");

        if (dto.Price <= 0)
            return Result<ServiceDTO>.Fail("Invalid Price");

        var entity = _mapper.Map<Service>(dto);

        await _uow.Services.AddAsync(entity);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        return Result<ServiceDTO>.Ok(_mapper.Map<ServiceDTO>(entity));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<ServiceDTO>> Update(int id, ServiceDTO dto)
    {
        var service = await _uow.Services.GetByIdAsync(id);

        if (service == null)
            return Result<ServiceDTO>.Ok(null);

        _mapper.Map(dto, service);
        service.LastUpdatedAt = DateTime.UtcNow;

        _uow.Services.Update(service);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        return Result<ServiceDTO>.Ok(_mapper.Map<ServiceDTO>(service));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<ServiceDTO>> Delete(int id)
    {
        var service = await _uow.Services.GetByIdAsync(id);

        if (service == null)
            return Result<ServiceDTO>.Ok(null);

        _uow.Services.Delete(service);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("services", _hub, "ServicesChanged");

        return Result<ServiceDTO>.Ok(null);
    }
}