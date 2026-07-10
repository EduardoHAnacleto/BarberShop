using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Moq;
using System.Linq.Expressions;

namespace BarberShop.Tests.Services;

public class ReportsServiceTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IShopClock> _clock;
    private readonly ReportsService _sut;

    private static readonly DateTime Now = new(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);

    public ReportsServiceTests()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);

        _clock = new Mock<IShopClock>();
        _clock.Setup(c => c.Now).Returns(Now);

        _sut = new ReportsService(_uow.Object, _clock.Object);
    }

    private static Worker MakeWorker(int id, string name) => new() { Id = id, Name = name };
    private static Service MakeService(int id, string name, decimal price) =>
        new() { Id = id, Name = name, Price = price, Duration = 30 };

    private void SetupAppointments(List<Appointment> appointments)
    {
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointments);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenNoAppointments_ReturnsZeroedSummary()
    {
        SetupAppointments([]);

        var result = await _sut.GetSummaryAsync();

        result.TotalRevenue.Should().Be(0);
        result.RevenueLast30Days.Should().Be(0);
        result.CompletedCount.Should().Be(0);
        result.CancelledCount.Should().Be(0);
        result.CancellationRate.Should().Be(0);
        result.TopServicesByRevenue.Should().BeEmpty();
        result.TopWorkersByRevenue.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummaryAsync_SumsRevenueFromCompletedAppointmentsOnly()
    {
        var worker = MakeWorker(1, "James Carter");
        var service = MakeService(1, "Haircut", 25.00m);

        SetupAppointments([
            new() { Id = 1, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now.AddDays(-1) },
            new() { Id = 2, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Scheduled, ScheduledFor = Now.AddDays(1) },
            new() { Id = 3, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Cancelled, ScheduledFor = Now.AddDays(-2) },
        ]);

        var result = await _sut.GetSummaryAsync();

        // Only the one Completed appointment counts toward revenue.
        result.TotalRevenue.Should().Be(25.00m);
        result.CompletedCount.Should().Be(1);
        result.CancelledCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesCompletedAppointmentsOlderThan30DaysFromRevenueLast30Days()
    {
        var worker = MakeWorker(1, "James Carter");
        var service = MakeService(1, "Haircut", 25.00m);

        SetupAppointments([
            new() { Id = 1, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now.AddDays(-5) },
            new() { Id = 2, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now.AddDays(-45) },
        ]);

        var result = await _sut.GetSummaryAsync();

        result.TotalRevenue.Should().Be(50.00m);
        result.RevenueLast30Days.Should().Be(25.00m);
    }

    [Theory]
    [InlineData(3, 1, 0.25)]
    [InlineData(0, 0, 0)]
    [InlineData(5, 0, 0)]
    public async Task GetSummaryAsync_ComputesCancellationRate(
        int completedCount, int cancelledCount, double expectedRate)
    {
        var worker = MakeWorker(1, "James Carter");
        var service = MakeService(1, "Haircut", 25.00m);

        var appointments = new List<Appointment>();
        for (var i = 0; i < completedCount; i++)
            appointments.Add(new Appointment { Id = appointments.Count + 1, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now });
        for (var i = 0; i < cancelledCount; i++)
            appointments.Add(new Appointment { Id = appointments.Count + 1, Worker = worker, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Cancelled, ScheduledFor = Now });

        SetupAppointments(appointments);

        var result = await _sut.GetSummaryAsync();

        result.CancellationRate.Should().Be(expectedRate);
    }

    [Fact]
    public async Task GetSummaryAsync_RanksTopServicesByRevenueDescending()
    {
        var worker = MakeWorker(1, "James Carter");
        var haircut = MakeService(1, "Haircut", 25.00m);
        var beard = MakeService(2, "Beard Trim", 15.00m);

        SetupAppointments([
            new() { Id = 1, Worker = worker, WorkerId = 1, Service = haircut, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now },
            new() { Id = 2, Worker = worker, WorkerId = 1, Service = haircut, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now },
            new() { Id = 3, Worker = worker, WorkerId = 1, Service = beard, ServiceId = 2, Status = Status.Completed, ScheduledFor = Now },
        ]);

        var result = await _sut.GetSummaryAsync();

        result.TopServicesByRevenue.Should().HaveCount(2);
        result.TopServicesByRevenue[0].ServiceName.Should().Be("Haircut");
        result.TopServicesByRevenue[0].Revenue.Should().Be(50.00m);
        result.TopServicesByRevenue[0].CompletedCount.Should().Be(2);
        result.TopServicesByRevenue[1].ServiceName.Should().Be("Beard Trim");
        result.TopServicesByRevenue[1].Revenue.Should().Be(15.00m);
    }

    [Fact]
    public async Task GetSummaryAsync_RanksTopWorkersByRevenueDescending()
    {
        var james = MakeWorker(1, "James Carter");
        var olivia = MakeWorker(2, "Olivia Bennett");
        var service = MakeService(1, "Haircut", 25.00m);

        SetupAppointments([
            new() { Id = 1, Worker = james, WorkerId = 1, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now },
            new() { Id = 2, Worker = olivia, WorkerId = 2, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now },
            new() { Id = 3, Worker = olivia, WorkerId = 2, Service = service, ServiceId = 1, Status = Status.Completed, ScheduledFor = Now },
        ]);

        var result = await _sut.GetSummaryAsync();

        result.TopWorkersByRevenue.Should().HaveCount(2);
        result.TopWorkersByRevenue[0].WorkerName.Should().Be("Olivia Bennett");
        result.TopWorkersByRevenue[0].Revenue.Should().Be(50.00m);
        result.TopWorkersByRevenue[1].WorkerName.Should().Be("James Carter");
        result.TopWorkersByRevenue[1].Revenue.Should().Be(25.00m);
    }

    [Fact]
    public async Task GetSummaryAsync_LimitsTopListsToFive()
    {
        var worker = MakeWorker(1, "James Carter");
        var appointments = new List<Appointment>();
        for (var i = 1; i <= 7; i++)
        {
            var service = MakeService(i, $"Service {i}", i * 10.00m);
            appointments.Add(new Appointment { Id = i, Worker = worker, WorkerId = 1, Service = service, ServiceId = i, Status = Status.Completed, ScheduledFor = Now });
        }
        SetupAppointments(appointments);

        var result = await _sut.GetSummaryAsync();

        result.TopServicesByRevenue.Should().HaveCount(5);
        // Highest-priced services (Service 7 downwards) should be first.
        result.TopServicesByRevenue[0].ServiceName.Should().Be("Service 7");
    }
}
