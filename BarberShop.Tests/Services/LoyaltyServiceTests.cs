using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BarberShop.Tests.Services;

public class LoyaltyServiceTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;

    public LoyaltyServiceTests()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);
    }

    private static IConfiguration ConfigWithThreshold(int? threshold)
    {
        var data = threshold.HasValue
            ? new Dictionary<string, string?> { ["Loyalty:VisitsForReward"] = threshold.Value.ToString() }
            : new Dictionary<string, string?>();
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static List<Appointment> Appointments(params Status[] statuses) =>
        statuses.Select((s, i) => new Appointment { Id = i + 1, CustomerId = 1, Status = s }).ToList();

    private LoyaltyService MakeSut(int? threshold = 10) =>
        new(_uow.Object, ConfigWithThreshold(threshold));

    [Fact]
    public async Task GetStatusAsync_WithNoCompletedAppointments_ReturnsFullDistanceToReward()
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1)).ReturnsAsync(Appointments());

        var result = await MakeSut(10).GetStatusAsync(1);

        result.CompletedVisits.Should().Be(0);
        result.VisitsForReward.Should().Be(10);
        result.VisitsUntilReward.Should().Be(10);
        result.RewardReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_CountsOnlyCompletedAppointments()
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1)).ReturnsAsync(
            Appointments(Status.Completed, Status.Completed, Status.Scheduled, Status.Cancelled, Status.Deleted));

        var result = await MakeSut(10).GetStatusAsync(1);

        result.CompletedVisits.Should().Be(2);
        result.VisitsUntilReward.Should().Be(8);
        result.RewardReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_WhenAppointmentsListIsNull_ReturnsZeroProgress()
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1)).ReturnsAsync((List<Appointment>?)null);

        var result = await MakeSut(10).GetStatusAsync(1);

        result.CompletedVisits.Should().Be(0);
        result.VisitsUntilReward.Should().Be(10);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(20, 10)]
    public async Task GetStatusAsync_WhenCompletedIsExactMultipleOfThreshold_ReturnsRewardReady(
        int completedCount, int threshold)
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1))
            .ReturnsAsync(Appointments(Enumerable.Repeat(Status.Completed, completedCount).ToArray()));

        var result = await MakeSut(threshold).GetStatusAsync(1);

        result.RewardReady.Should().BeTrue();
        result.VisitsUntilReward.Should().Be(0);
    }

    [Fact]
    public async Task GetStatusAsync_UsesConfiguredThreshold()
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1))
            .ReturnsAsync(Appointments(Status.Completed, Status.Completed, Status.Completed));

        var result = await MakeSut(5).GetStatusAsync(1);

        result.VisitsForReward.Should().Be(5);
        result.VisitsUntilReward.Should().Be(2);
    }

    [Fact]
    public async Task GetStatusAsync_WhenThresholdNotConfigured_DefaultsToTen()
    {
        _appointmentRepo.Setup(r => r.GetByCustomer(1)).ReturnsAsync(Appointments());

        var result = await MakeSut(null).GetStatusAsync(1);

        result.VisitsForReward.Should().Be(10);
    }
}
