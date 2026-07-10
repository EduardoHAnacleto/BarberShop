using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace BarberShop.Tests.Services;

public class AppointmentReminderServiceTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IEmailService> _email;
    private readonly Mock<IShopClock> _clock;
    private readonly AppointmentReminderService _sut;

    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    public AppointmentReminderServiceTests()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _email = new Mock<IEmailService>();
        _email
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _clock = new Mock<IShopClock>();
        _clock.Setup(c => c.Now).Returns(Now);

        _sut = new AppointmentReminderService(
            _uow.Object, _email.Object, _clock.Object, NullLogger<AppointmentReminderService>.Instance);
    }

    private static Appointment MakeAppointment(
        int id, DateTime scheduledFor, DateTime? reminder24h = null, DateTime? reminder1h = null) => new()
        {
            Id = id,
            WorkerId = 1,
            Worker = new Worker { Id = 1, Name = "James Carter" },
            CustomerId = 1,
            Customer = new Customer { Id = 1, Name = "Emily Johnson", Email = "emily@example.com" },
            ServiceId = 1,
            Service = new Service { Id = 1, Name = "Haircut", Duration = 30 },
            Status = Status.Scheduled,
            ScheduledFor = scheduledFor,
            Reminder24hSentAt = reminder24h,
            Reminder1hSentAt = reminder1h,
        };

    private void SetupCandidates(List<Appointment> for24h, List<Appointment> for1h)
    {
        var call = 0;
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(() => ++call == 1 ? for24h : for1h);
    }

    [Fact]
    public async Task SendDueRemindersAsync_Sends24hReminder_ForAppointmentNotYetReminded()
    {
        var appt = MakeAppointment(1, Now.AddHours(23.5));
        SetupCandidates([appt], []);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(1);
        appt.Reminder24hSentAt.Should().NotBeNull();
        _email.Verify(e => e.SendAsync(
            "emily@example.com", "Emily Johnson", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task SendDueRemindersAsync_SkipsAppointment_WhenAlready24hReminded()
    {
        var appt = MakeAppointment(1, Now.AddHours(23.5), reminder24h: Now.AddMinutes(-5));
        SetupCandidates([appt], []);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(0);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task SendDueRemindersAsync_Sends1hReminder_ForAppointmentNotYetReminded()
    {
        var appt = MakeAppointment(1, Now.AddMinutes(60));
        SetupCandidates([], [appt]);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(1);
        appt.Reminder1hSentAt.Should().NotBeNull();
        _email.Verify(e => e.SendAsync(
            "emily@example.com", "Emily Johnson", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SendDueRemindersAsync_SkipsAppointment_WhenAlready1hReminded()
    {
        var appt = MakeAppointment(1, Now.AddMinutes(60), reminder1h: Now.AddMinutes(-2));
        SetupCandidates([], [appt]);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(0);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SendDueRemindersAsync_SendsBothWindowsIndependently_AndSumsTheCount()
    {
        var appt24h = MakeAppointment(1, Now.AddHours(23.5));
        var appt1h = MakeAppointment(2, Now.AddMinutes(60));
        SetupCandidates([appt24h], [appt1h]);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(2);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendDueRemindersAsync_WhenNothingDue_ReturnsZeroAndNeverSaves()
    {
        SetupCandidates([], []);

        var count = await _sut.SendDueRemindersAsync();

        count.Should().Be(0);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task SendDueRemindersAsync_ReminderEmailBody_MentionsServiceAndWorker()
    {
        var appt = MakeAppointment(1, Now.AddHours(23.5));
        SetupCandidates([appt], []);

        await _sut.SendDueRemindersAsync();

        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("Haircut") && body.Contains("James Carter"))),
            Times.Once);
    }
}
