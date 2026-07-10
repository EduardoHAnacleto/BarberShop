using BarberShop.Application.Interfaces;

namespace BarberShop.API.Services;

// Periodically sweeps for due appointment reminders. IAppointmentReminderService
// (and the IUnitOfWork/IEmailService it depends on) are Scoped, so this
// Singleton-lifetime hosted service resolves them through a fresh
// IServiceScope on every tick rather than injecting them directly.
public class AppointmentReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AppointmentReminderBackgroundService> _logger;

    public AppointmentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AppointmentReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Reminders:Enabled", true))
        {
            _logger.LogInformation("Appointment reminders disabled (Reminders:Enabled=false)");
            return;
        }

        var intervalMinutes = _config.GetValue("Reminders:IntervalMinutes", 10);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        _logger.LogInformation(
            "Appointment reminder sweep started (every {Minutes} min)", intervalMinutes);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IAppointmentReminderService>();
                var sent = await reminders.SendDueRemindersAsync();

                if (sent > 0)
                    _logger.LogInformation("Reminder sweep sent {Count} email(s)", sent);
            }
            catch (Exception ex)
            {
                // A single failed sweep (e.g. transient DB hiccup) must not kill
                // the loop — the next tick tries again.
                _logger.LogError(ex, "Appointment reminder sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
