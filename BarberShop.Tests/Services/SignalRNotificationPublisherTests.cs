using BarberShop.API.Hubs;
using BarberShop.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class SignalRNotificationPublisherTests
{
    // =========================
    // SETUP
    // =========================
    // One mocked IClientProxy per hub — SendAsync(method, args) is a static
    // extension over SendCoreAsync, so verifying the real send means mocking
    // the interface method underneath it, not the extension itself.
    private readonly Mock<IClientProxy> _workersClients = new();
    private readonly Mock<IClientProxy> _servicesClients = new();
    private readonly Mock<IClientProxy> _customersClients = new();
    private readonly Mock<IClientProxy> _appointmentsClients = new();
    private readonly Mock<IClientProxy> _usersClients = new();
    private readonly Mock<IClientProxy> _reviewsClients = new();
    private readonly Mock<IClientProxy> _scheduleClients = new();
    private readonly Mock<IClientProxy> _workerSchedulesClients = new();

    private readonly SignalRNotificationPublisher _sut;

    public SignalRNotificationPublisherTests()
    {
        var workersHub = MakeHubContext<WorkersHub>(_workersClients);
        var servicesHub = MakeHubContext<ServicesHub>(_servicesClients);
        var customersHub = MakeHubContext<CustomersHub>(_customersClients);
        var appointmentsHub = MakeHubContext<AppointmentsHub>(_appointmentsClients);
        var usersHub = MakeHubContext<UsersHub>(_usersClients);
        var reviewsHub = MakeHubContext<ReviewsHub>(_reviewsClients);
        var scheduleHub = MakeHubContext<ScheduleHub>(_scheduleClients);
        var workerSchedulesHub = MakeHubContext<WorkerSchedulesHub>(_workerSchedulesClients);

        _sut = new SignalRNotificationPublisher(
            workersHub, servicesHub, customersHub, appointmentsHub, usersHub,
            reviewsHub, scheduleHub, workerSchedulesHub,
            NullLogger<SignalRNotificationPublisher>.Instance);
    }

    private static IHubContext<THub> MakeHubContext<THub>(Mock<IClientProxy> clientsMock) where THub : Hub
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(clientsMock.Object);

        var hubContext = new Mock<IHubContext<THub>>();
        hubContext.Setup(h => h.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    // =========================
    // ROUTING — every known channel reaches its own hub, and only that hub
    // =========================
    [Theory]
    [InlineData("workers", "WorkersChanged")]
    [InlineData("services", "ServicesChanged")]
    [InlineData("customers", "CustomersChanged")]
    [InlineData("appointments", "AppointmentsChanged")]
    [InlineData("users", "UsersChanged")]
    [InlineData("reviews", "ReviewsChanged")]
    [InlineData("schedule", "ScheduleChanged")]
    [InlineData("worker-schedules", "WorkerSchedulesChanged")]
    public async Task PublishAsync_SendsToTheHubMatchingTheChannel(string channel, string eventName)
    {
        await _sut.PublishAsync(channel, eventName);

        var allClients = new[]
        {
            _workersClients, _servicesClients, _customersClients, _appointmentsClients,
            _usersClients, _reviewsClients, _scheduleClients, _workerSchedulesClients,
        };

        foreach (var clients in allClients)
        {
            clients.Verify(
                c => c.SendCoreAsync(eventName, It.IsAny<object[]>(), default),
                channel == "workers" && clients == _workersClients ? Times.Once
                    : channel == "services" && clients == _servicesClients ? Times.Once
                    : channel == "customers" && clients == _customersClients ? Times.Once
                    : channel == "appointments" && clients == _appointmentsClients ? Times.Once
                    : channel == "users" && clients == _usersClients ? Times.Once
                    : channel == "reviews" && clients == _reviewsClients ? Times.Once
                    : channel == "schedule" && clients == _scheduleClients ? Times.Once
                    : channel == "worker-schedules" && clients == _workerSchedulesClients ? Times.Once
                    : Times.Never);
        }
    }

    // =========================
    // UNKNOWN CHANNEL — silently no-ops (logs a warning) instead of throwing
    // =========================
    [Fact]
    public async Task PublishAsync_UnknownChannel_DoesNotBroadcastToAnyHub()
    {
        await _sut.PublishAsync("not-a-real-channel", "SomethingChanged");

        _reviewsClients.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
        _scheduleClients.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
        _workerSchedulesClients.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
    }
}
