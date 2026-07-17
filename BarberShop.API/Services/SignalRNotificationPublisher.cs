using BarberShop.API.Hubs;
using BarberShop.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BarberShop.API.Services;

public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<WorkersHub> _workersHub;
    private readonly IHubContext<ServicesHub> _servicesHub;
    private readonly IHubContext<CustomersHub> _customersHub;
    private readonly IHubContext<AppointmentsHub> _appointmentsHub;
    private readonly IHubContext<UsersHub> _usersHub;
    private readonly IHubContext<ReviewsHub> _reviewsHub;
    private readonly IHubContext<ScheduleHub> _scheduleHub;
    private readonly IHubContext<WorkerSchedulesHub> _workerSchedulesHub;
    private readonly ILogger<SignalRNotificationPublisher> _logger;

    public SignalRNotificationPublisher(
        IHubContext<WorkersHub> workersHub,
        IHubContext<ServicesHub> servicesHub,
        IHubContext<CustomersHub> customersHub,
        IHubContext<AppointmentsHub> appointmentsHub,
        IHubContext<UsersHub> usersHub,
        IHubContext<ReviewsHub> reviewsHub,
        IHubContext<ScheduleHub> scheduleHub,
        IHubContext<WorkerSchedulesHub> workerSchedulesHub,
        ILogger<SignalRNotificationPublisher> logger)
    {
        _workersHub = workersHub;
        _servicesHub = servicesHub;
        _customersHub = customersHub;
        _appointmentsHub = appointmentsHub;
        _usersHub = usersHub;
        _reviewsHub = reviewsHub;
        _scheduleHub = scheduleHub;
        _workerSchedulesHub = workerSchedulesHub;
        _logger = logger;
    }

    public async Task PublishAsync(string channel, string eventName)
    {
        var clients = channel switch
        {
            "workers"          => _workersHub.Clients,
            "services"         => _servicesHub.Clients,
            "customers"        => _customersHub.Clients,
            "appointments"     => _appointmentsHub.Clients,
            "users"            => _usersHub.Clients,
            "reviews"          => _reviewsHub.Clients,
            "schedule"         => _scheduleHub.Clients,
            "worker-schedules" => _workerSchedulesHub.Clients,
            _                  => null
        };

        if (clients is null)
        {
            _logger.LogWarning(
                "No SignalR hub registered for channel '{Channel}' — event '{EventName}' was not broadcast",
                channel, eventName);
            return;
        }

        await clients.All.SendAsync(eventName);
    }
}
