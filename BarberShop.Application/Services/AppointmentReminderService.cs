using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;

namespace BarberShop.Application.Services;

// Core "one sweep" logic behind the reminder background job. Kept separate
// from the BackgroundService/timer plumbing so it can be unit tested like
// every other service in this project instead of only via integration tests.
public class AppointmentReminderService : IAppointmentReminderService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _email;
    private readonly IShopClock _clock;
    private readonly ILogger<AppointmentReminderService> _logger;

    public AppointmentReminderService(
        IUnitOfWork uow, IEmailService email, IShopClock clock, ILogger<AppointmentReminderService> logger)
    {
        _uow = uow;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SendDueRemindersAsync()
    {
        var now = _clock.Now;

        var sent24h = await SendWindowAsync(
            windowStart: now.AddHours(23), windowEnd: now.AddHours(24),
            alreadySent: a => a.Reminder24hSentAt != null,
            markSent: a => a.Reminder24hSentAt = DateTime.UtcNow,
            whenLabel: "tomorrow");

        var sent1h = await SendWindowAsync(
            windowStart: now.AddMinutes(50), windowEnd: now.AddMinutes(70),
            alreadySent: a => a.Reminder1hSentAt != null,
            markSent: a => a.Reminder1hSentAt = DateTime.UtcNow,
            whenLabel: "in about an hour");

        return sent24h + sent1h;
    }

    // Fetches Scheduled appointments in [windowStart, windowEnd], skips the
    // ones already reminded for this window, emails the rest and persists
    // the mark in one batch save.
    private async Task<int> SendWindowAsync(
        DateTime windowStart,
        DateTime windowEnd,
        Func<Appointment, bool> alreadySent,
        Action<Appointment> markSent,
        string whenLabel)
    {
        var candidates = await _uow.Appointments.GetAllAsync(
            a => a.Status == Status.Scheduled && a.ScheduledFor >= windowStart && a.ScheduledFor <= windowEnd,
            includes: [a => a.Customer, a => a.Worker, a => a.Service]);

        var due = candidates.Where(a => !alreadySent(a)).ToList();

        foreach (var appointment in due)
        {
            await _email.SendAsync(
                appointment.Customer.Email,
                appointment.Customer.Name,
                $"Reminder: your {appointment.Service.Name} appointment is {whenLabel}",
                $"""
                <p>Hi {appointment.Customer.Name},</p>
                <p>This is a reminder that your <strong>{appointment.Service.Name}</strong> appointment
                with {appointment.Worker.Name} is {whenLabel}, at {appointment.ScheduledFor:MMM d, h:mm tt}.</p>
                <p>See you soon!</p>
                """);

            markSent(appointment);
            _uow.Appointments.Update(appointment);
        }

        if (due.Count > 0)
        {
            await _uow.SaveAsync();
            _logger.LogInformation(
                "Sent {Count} reminder(s) for window [{Start}, {End}]", due.Count, windowStart, windowEnd);
        }

        return due.Count;
    }
}
