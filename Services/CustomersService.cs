using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Services;

public class CustomersService : ICustomersService
{
    private readonly ICustomerRepository _repository;
    private readonly IMapper _mapper;
    private readonly AppDbContext _context;
    private readonly RedisService _redis;
    private readonly IHubContext<CustomersHub> _hubContext;

    public CustomersService(
        ICustomerRepository repository,
        IMapper mapper,
        AppDbContext context,
        RedisService redis,
        IHubContext<CustomersHub> hubContext)
    {
        _repository = repository;
        _mapper = mapper;
        _context = context;
        _redis = redis;
        _hubContext = hubContext;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<CustomerDTO>> GetAllAsync()
    {
        var cacheKey = "customers:all";

        var cached = await _redis.GetAsync<List<CustomerDTO>>(cacheKey);
        if (cached != null)
            return cached;

        var customers = await _repository.GetAllAsync();

        var dtoList = _mapper.Map<List<CustomerDTO>>(customers);

        await _redis.SetAsync(cacheKey, dtoList, TimeSpan.FromMinutes(10));

        return dtoList;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<CustomerDTO?> GetByIdAsync(int id)
    {
        var cacheKey = $"customers:{id}";

        var cached = await _redis.GetAsync<CustomerDTO>(cacheKey);
        if (cached != null)
            return cached;

        var customer = await _repository.GetByIdAsync(id);

        if (customer == null)
            return null;

        var dto = _mapper.Map<CustomerDTO>(customer);

        await _redis.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        return dto;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<CustomerDTO>> Create(CustomerDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<CustomerDTO>.Fail("Invalid Name");

        var customer = _mapper.Map<Customer>(dto);

        await _repository.AddAsync(customer);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("customers");
        await _hubContext.Clients.All.SendAsync("CustomersChanged");

        var resultDto = _mapper.Map<CustomerDTO>(customer);

        return Result<CustomerDTO>.Ok(resultDto);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<CustomerDTO>> Update(int id, CustomerDTO dto)
    {
        var customer = await _repository.GetByIdAsync(id);

        if (customer == null)
            return Result<CustomerDTO>.Ok(null);

        _mapper.Map(dto, customer);
        customer.LastUpdatedAt = DateTime.UtcNow;

        _repository.Update(customer);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("customers");
        await _hubContext.Clients.All.SendAsync("CustomersChanged");

        var resultDto = _mapper.Map<CustomerDTO>(customer);

        return Result<CustomerDTO>.Ok(resultDto);
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<CustomerDTO>> Delete(int id)
    {
        var customer = await _repository.GetByIdAsync(id);

        if (customer == null)
            return Result<CustomerDTO>.Ok(null);

        _repository.Delete(customer);
        await _context.SaveChangesAsync();

        await _redis.InvalidateByPrefixAsync("customers");
        await _hubContext.Clients.All.SendAsync("CustomersChanged");

        return Result<CustomerDTO>.Ok(null);
    }
}