using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BarberShop.Application.Services;

public class WaitlistService : IWaitlistService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _email;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(IUnitOfWork uow, IEmailService email, ILogger<WaitlistService> logger)
    {
        _uow = uow;
        _email = email;
        _logger = logger;
    }

    public async Task<Result<WaitlistResponseDTO>> Join(int customerId, WaitlistRequestDTO dto)
    {
        var worker = await _uow.Workers.GetByIdAsync(dto.WorkerId);
        if (worker == null)
            return Result<WaitlistResponseDTO>.Fail("Worker not found");

        var service = await _uow.Services.GetByIdAsync(dto.ServiceId);
        if (service == null)
            return Result<WaitlistResponseDTO>.Fail("Service not found");

        var customer = await _uow.Customers.GetByIdAsync(customerId);
        if (customer == null)
            return Result<WaitlistResponseDTO>.Fail("Customer not found");

        var preferredDate = dto.PreferredDate.Date;

        var existing = await _uow.Waitlist.GetAllAsync(w =>
            w.CustomerId == customerId && w.WorkerId == dto.WorkerId &&
            w.ServiceId == dto.ServiceId && w.PreferredDate == preferredDate);

        if (existing.Count > 0)
        {
            _logger.LogInformation(
                "Waitlist join rejected — customer {CustomerId} already waiting for WorkerId {WorkerId} on {Date}",
                customerId, dto.WorkerId, preferredDate);
            return Result<WaitlistResponseDTO>.Fail("You're already on the waitlist for this day");
        }

        var entry = new Waitlist
        {
            CustomerId = customerId,
            Customer = customer,
            WorkerId = dto.WorkerId,
            Worker = worker,
            ServiceId = dto.ServiceId,
            Service = service,
            PreferredDate = preferredDate,
            CreatedAt = DateTime.UtcNow,
        };

        await _uow.Waitlist.AddAsync(entry);
        await _uow.SaveAsync();

        _logger.LogInformation(
            "Customer {CustomerId} joined the waitlist for WorkerId {WorkerId} on {Date}",
            customerId, dto.WorkerId, preferredDate);

        return Result<WaitlistResponseDTO>.Ok(MapToDto(entry));
    }

    public async Task<List<WaitlistResponseDTO>> GetMineAsync(int customerId)
    {
        var entries = await _uow.Waitlist.GetAllAsync(
            w => w.CustomerId == customerId,
            includes: [w => w.Customer, w => w.Worker, w => w.Service]);

        return entries.Select(MapToDto).ToList();
    }

    public async Task<List<WaitlistResponseDTO>> GetAllAsync()
    {
        var entries = await _uow.Waitlist.GetAllAsync(
            includes: [w => w.Customer, w => w.Worker, w => w.Service]);

        return entries.Select(MapToDto).ToList();
    }

    public async Task<Result<bool>> Leave(int customerId, int waitlistId)
    {
        var entry = await _uow.Waitlist.GetByIdAsync(waitlistId);
        if (entry == null)
            return Result<bool>.Fail("Waitlist entry not found");

        if (entry.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Waitlist leave rejected — customer {CustomerId} does not own entry {WaitlistId}",
                customerId, waitlistId);
            return Result<bool>.Fail("You can only remove your own waitlist entry");
        }

        _uow.Waitlist.Delete(entry);
        await _uow.SaveAsync();

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> Delete(int id)
    {
        var entry = await _uow.Waitlist.GetByIdAsync(id);
        if (entry == null)
            return Result<bool>.Fail("Waitlist entry not found");

        _uow.Waitlist.Delete(entry);
        await _uow.SaveAsync();

        return Result<bool>.Ok(true);
    }

    public async Task<int> NotifyWaitlistForAsync(int workerId, DateTime date)
    {
        var preferredDate = date.Date;

        var candidates = await _uow.Waitlist.GetAllAsync(
            w => w.WorkerId == workerId && w.PreferredDate == preferredDate,
            includes: [w => w.Customer, w => w.Worker, w => w.Service]);

        var due = candidates.Where(w => w.NotifiedAt == null).ToList();

        foreach (var entry in due)
        {
            await _email.SendAsync(
                entry.Customer.Email,
                entry.Customer.Name,
                $"A slot opened up with {entry.Worker.Name}",
                $"""
                <p>Hi {entry.Customer.Name},</p>
                <p>Good news — a slot may have opened up with <strong>{entry.Worker.Name}</strong>
                on {preferredDate:MMM d, yyyy} for your requested {entry.Service.Name}.</p>
                <p>Slots fill up fast — head back to the booking page to grab it.</p>
                """);

            entry.NotifiedAt = DateTime.UtcNow;
            _uow.Waitlist.Update(entry);
        }

        if (due.Count > 0)
        {
            await _uow.SaveAsync();
            _logger.LogInformation(
                "Notified {Count} waitlist entr{Suffix} for WorkerId {WorkerId} on {Date}",
                due.Count, due.Count == 1 ? "y" : "ies", workerId, preferredDate);
        }

        return due.Count;
    }

    private static WaitlistResponseDTO MapToDto(Waitlist w) => new()
    {
        Id = w.Id,
        CustomerId = w.CustomerId,
        CustomerName = w.Customer.Name,
        WorkerId = w.WorkerId,
        WorkerName = w.Worker.Name,
        ServiceId = w.ServiceId,
        ServiceName = w.Service.Name,
        PreferredDate = w.PreferredDate,
        CreatedAt = w.CreatedAt,
        Notified = w.NotifiedAt != null,
    };
}
