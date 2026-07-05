using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BarberShop.Application.Services;

public class CustomersService : BaseService, ICustomersService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.CustomersService");

    private static readonly Meter _meter =
        new("BarberShop.CustomersService");

    private static readonly Counter<long> _customersCreated =
        _meter.CreateCounter<long>(
            "barbershop.customers.created",
            description: "Total number of customers created");

    private static readonly Counter<long> _customersDeleted =
        _meter.CreateCounter<long>(
            "barbershop.customers.deleted",
            description: "Total number of customers deleted");

    private static readonly Histogram<double> _operationDuration =
        _meter.CreateHistogram<double>(
            "barbershop.customers.operation_duration",
            unit: "ms",
            description: "Duration of customer operations");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<CustomersService> _logger;

    public CustomersService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        INotificationPublisher notifications,
        ILogger<CustomersService> logger) : base(redis, notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<CustomerDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllCustomers");

        _logger.LogInformation("Fetching all customers");

        var result = await GetCachedAsync(
            "customers:all",
            async () => _mapper.Map<List<CustomerDTO>>(await _uow.Customers.GetAllAsync())
        );

        var count = result?.Count ?? 0;
        span?.SetTag("customers.count", count);
        _logger.LogInformation("Fetched {Count} customers", count);

        return result ?? [];
    }

    // =========================
    // GET ALL PAGED
    // =========================
    public async Task<PagedResult<CustomerDTO>> GetAllAsync(PaginationParams pagination)
    {
        using var span = _activitySource.StartActivity("GetAllCustomersPaged");
        span?.SetTag("pagination.page", pagination.Page);
        span?.SetTag("pagination.pageSize", pagination.PageSize);

        _logger.LogInformation(
            "Fetching customers page {Page} (size {PageSize})",
            pagination.Page, pagination.PageSize);

        var paged = await _uow.Customers.GetPagedAsync(pagination);

        var mapped = PagedResult<CustomerDTO>.Create(
            _mapper.Map<List<CustomerDTO>>(paged.Items),
            paged.TotalCount,
            pagination);

        span?.SetTag("customers.totalCount", mapped.TotalCount);

        _logger.LogInformation(
            "Fetched page {Page}/{TotalPages} ({TotalCount} total customers)",
            mapped.Page, mapped.TotalPages, mapped.TotalCount);

        return mapped;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<CustomerDTO?> GetByIdAsync(int id)
    {
        using var span = _activitySource.StartActivity("GetCustomerById");
        span?.SetTag("customer.id", id);

        _logger.LogInformation("Fetching customer {CustomerId}", id);

        var result = await GetCachedAsync(
            $"customers:{id}",
            async () =>
            {
                var customer = await _uow.Customers.GetByIdAsync(id);
                return customer == null ? null : _mapper.Map<CustomerDTO>(customer);
            }
        );

        if (result == null)
            _logger.LogWarning("Customer {CustomerId} not found", id);

        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<CustomerDTO>> Create(CustomerDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateCustomer");
        span?.SetTag("customer.name", dto.Name);
        span?.SetTag("customer.email", dto.Email);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating customer {CustomerName} with email {Email}",
            dto.Name, dto.Email);

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogWarning("Customer creation failed — invalid name");
            return Result<CustomerDTO>.Fail("Invalid Name");
        }

        var customer = _mapper.Map<Customer>(dto);

        await _uow.Customers.AddAsync(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", "CustomersChanged");

        stopwatch.Stop();

        span?.SetTag("customer.id", customer.Id);
        _customersCreated.Add(1);
        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "create" } });

        _logger.LogInformation(
            "Customer {CustomerId} ({CustomerName}) created in {ElapsedMs}ms",
            customer.Id, customer.Name, stopwatch.Elapsed.TotalMilliseconds);

        return Result<CustomerDTO>.Ok(_mapper.Map<CustomerDTO>(customer));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<CustomerDTO>> Update(int id, CustomerDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateCustomer");
        span?.SetTag("customer.id", id);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Updating customer {CustomerId}", id);

        var customer = await _uow.Customers.GetByIdAsync(id);

        if (customer == null)
        {
            _logger.LogWarning("Customer {CustomerId} not found for update", id);
            return Result<CustomerDTO>.Ok(null);
        }

        _mapper.Map(dto, customer);
        customer.LastUpdatedAt = DateTime.UtcNow;

        _uow.Customers.Update(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", "CustomersChanged");

        stopwatch.Stop();

        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "update" } });

        _logger.LogInformation(
            "Customer {CustomerId} updated in {ElapsedMs}ms",
            id, stopwatch.Elapsed.TotalMilliseconds);

        return Result<CustomerDTO>.Ok(_mapper.Map<CustomerDTO>(customer));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<CustomerDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteCustomer");
        span?.SetTag("customer.id", id);

        _logger.LogInformation("Deleting customer {CustomerId}", id);

        var customer = await _uow.Customers.GetByIdAsync(id);

        if (customer == null)
        {
            _logger.LogWarning("Customer {CustomerId} not found for deletion", id);
            return Result<CustomerDTO>.Ok(null);
        }

        _uow.Customers.Delete(customer);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("customers", "CustomersChanged");

        _customersDeleted.Add(1);

        _logger.LogInformation("Customer {CustomerId} deleted successfully", id);

        return Result<CustomerDTO>.Ok(null);
    }
}
