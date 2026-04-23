using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Application.Services;

public class CustomersService : BaseService, ICustomersService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IHubContext<CustomersHub> _hub;

    public CustomersService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        IHubContext<CustomersHub> hub) : base(redis)
    {

        _uow = uow;
        _mapper = mapper;
        _hub = hub;
    }

    public async Task<List<CustomerDTO>> GetAllAsync()
    {
        var result = await GetCachedAsync(
            "customers:all",
            async () => _mapper.Map<List<CustomerDTO>>(await _uow.Customers.GetAllAsync())
        );

        return result ?? [];
    }

    public async Task<CustomerDTO?> GetByIdAsync(int id)
    {
        return await GetCachedAsync(
            $"customers:{id}",
            async () =>
            {
                var customer = await _uow.Customers.GetByIdAsync(id);
                return customer == null ? null : _mapper.Map<CustomerDTO>(customer);
            }
        );
    }

    public async Task<Result<CustomerDTO>> Create(CustomerDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<CustomerDTO>.Fail("Invalid Name");

        var customer = _mapper.Map<Customer>(dto);

        await _uow.Customers.AddAsync(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", _hub, "CustomersChanged");

        return Result<CustomerDTO>.Ok(_mapper.Map<CustomerDTO>(customer));
    }

    public async Task<Result<CustomerDTO>> Update(int id, CustomerDTO dto)
    {
        var customer = await _uow.Customers.GetByIdAsync(id);

        if (customer == null)
            return Result<CustomerDTO>.Ok(null);

        _mapper.Map(dto, customer);
        customer.LastUpdatedAt = DateTime.UtcNow;

        _uow.Customers.Update(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", _hub, "CustomersChanged");

        return Result<CustomerDTO>.Ok(_mapper.Map<CustomerDTO>(customer));
    }

    public async Task<Result<CustomerDTO>> Delete(int id)
    {
        var customer = await _uow.Customers.GetByIdAsync(id);

        if (customer == null)
            return Result<CustomerDTO>.Ok(null);

        _uow.Customers.Delete(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", _hub, "CustomersChanged");

        return Result<CustomerDTO>.Ok(null);
    }
}