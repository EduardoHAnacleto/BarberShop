using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class AvailabilityServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWorkerRepository> _workerRepo;
    private readonly Mock<IServiceRepository> _serviceRepo;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IWorkingHoursService> _workingHours;
    private readonly Mock<IShopClock> _clock;
    private readonly AvailabilityService _sut;

    // Fixed "today" used across tests: Friday 2026-07-10, 08:00 shop time.
    private static readonly DateOnly Today = new(2026, 7, 10);
    private static readonly DateTime MorningNow = new(2026, 7, 10, 8, 0, 0);

    public AvailabilityServiceTests()
    {
        _workerRepo = new Mock<IWorkerRepository>();
        _serviceRepo = new Mock<IServiceRepository>();
        _appointmentRepo = new Mock<IAppointmentRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Workers).Returns(_workerRepo.Object);
        _uow.Setup(u => u.Services).Returns(_serviceRepo.Object);
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);

        _workingHours = new Mock<IWorkingHoursService>();
        // Default: standard business day 09:00–18:00 with a 12:00–13:00 break.
        _workingHours
            .Setup(w => w.GetScheduleByDayAsync(It.IsAny<DayOfWeek>()))
            .ReturnsAsync(MakeSchedule());
        // Default: no exceptional closures.
        _workingHours
            .Setup(w => w.GetEffectiveClosuresAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        _clock = new Mock<IShopClock>();
        _clock.Setup(c => c.Now).Returns(MorningNow);

        SetupWorker(1);
        SetupService(1, durationMinutes: 30);
        SetupAppointments();

        _sut = new AvailabilityService(
            _uow.Object,
            _workingHours.Object,
            _clock.Object,
            NullLogger<AvailabilityService>.Instance);
    }

    // =========================
    // HELPERS
    // =========================
    private static Application.DTOs.BusinessScheduleDTO MakeSchedule(
        bool isOpen = true,
        string open = "09:00",
        string close = "18:00",
        string? breakStart = "12:00",
        string? breakEnd = "13:00") => new()
        {
            Id = 5,
            DayOfWeek = Today.DayOfWeek,
            IsOpen = isOpen,
            OpenTime = TimeSpan.Parse(open),
            CloseTime = TimeSpan.Parse(close),
            BreakStart = breakStart != null ? TimeSpan.Parse(breakStart) : null,
            BreakEnd = breakEnd != null ? TimeSpan.Parse(breakEnd) : null,
        };

    private void SetupWorker(int id) =>
        _workerRepo
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(new Worker { Id = id, Name = "James Carter", Email = "j@b.com" });

    private void SetupService(int id, int durationMinutes) =>
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(new Service
            {
                Id = id,
                Name = "Haircut",
                Duration = durationMinutes,
                Price = 35,
            });

    private void SetupAppointments(params Appointment[] appointments) =>
        _appointmentRepo
            .Setup(r => r.GetByWorker(1))
            .ReturnsAsync(appointments.ToList());

    private static Appointment MakeAppointment(
        string time, int durationMinutes, Status status = Status.Scheduled) => new()
        {
            Id = 99,
            WorkerId = 1,
            CustomerId = 2,
            ServiceId = 7,
            ScheduledFor = DateTime.Parse($"2026-07-10T{time}:00"),
            Status = status,
            Service = new Service { Id = 7, Name = "Any", Duration = durationMinutes, Price = 10 },
        };

    // =========================
    // VALIDATION
    // =========================

    [Fact]
    public async Task GetAvailability_WhenWorkerNotFound_Fails()
    {
        var result = await _sut.GetAvailabilityAsync(42, Today, 1);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Worker not found");
    }

    [Fact]
    public async Task GetAvailability_WhenServiceNotFound_Fails()
    {
        var result = await _sut.GetAvailabilityAsync(1, Today, 42);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Service not found");
    }

    // =========================
    // SCHEDULE / CLOSURES
    // =========================

    [Fact]
    public async Task GetAvailability_WhenDayIsClosed_ReturnsNoSlots()
    {
        _workingHours
            .Setup(w => w.GetScheduleByDayAsync(Today.DayOfWeek))
            .ReturnsAsync(MakeSchedule(isOpen: false));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Success.Should().BeTrue();
        result.Data!.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailability_WhenFullDayClosure_ReturnsNoSlots()
    {
        _workingHours
            .Setup(w => w.GetEffectiveClosuresAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([(new DateTime(2026, 7, 9), new DateTime(2026, 7, 11))]);

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Data!.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailability_WhenPartialDayClosure_BlocksOnlyThatWindow()
    {
        // Shop exceptionally closes from 15:00 onward.
        _workingHours
            .Setup(w => w.GetEffectiveClosuresAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([(new DateTime(2026, 7, 10, 15, 0, 0), new DateTime(2026, 7, 10, 23, 59, 0))]);

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Data!.Slots.Should().Contain("14:30");
        result.Data.Slots.Should().NotContain("15:00");
        result.Data.Slots.Should().NotContain("17:30");
    }

    // =========================
    // GRID / BREAK / CLOSING TIME
    // =========================

    [Fact]
    public async Task GetAvailability_OpenDayNoAppointments_ReturnsGridMinusBreak()
    {
        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        var slots = result.Data!.Slots;
        // 09:00–18:00 every 30 min minus the 12:00–13:00 break = 16 slots.
        slots.Should().HaveCount(16);
        slots.Should().Contain(["09:00", "11:30", "13:00", "17:30"]);
        slots.Should().NotContain(["12:00", "12:30", "18:00"]);
    }

    [Fact]
    public async Task GetAvailability_LongService_CannotRunIntoBreakOrPastClose()
    {
        SetupService(3, durationMinutes: 50);

        var result = await _sut.GetAvailabilityAsync(1, Today, 3);

        var slots = result.Data!.Slots;
        // 11:30 + 50min = 12:20 → overlaps the 12:00 break start.
        slots.Should().NotContain("11:30");
        // 17:30 + 50min = 18:20 → past closing time.
        slots.Should().NotContain("17:30");
        slots.Should().Contain(["11:00", "17:00"]);
    }

    // =========================
    // EXISTING APPOINTMENTS
    // =========================

    [Fact]
    public async Task GetAvailability_OccupiedSlot_IsExcludedUsingItsOwnServiceDuration()
    {
        // Existing 60-min appointment at 14:00 occupies [14:00, 15:00).
        SetupAppointments(MakeAppointment("14:00", 60));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        var slots = result.Data!.Slots;
        slots.Should().NotContain(["14:00", "14:30"]);
        slots.Should().Contain(["13:30", "15:00"]);
    }

    [Fact]
    public async Task GetAvailability_RequestedDurationOverlappingOccupied_IsExcluded()
    {
        // 50-min service requested; existing appointment [14:00, 14:30).
        SetupService(3, durationMinutes: 50);
        SetupAppointments(MakeAppointment("14:00", 30));

        var result = await _sut.GetAvailabilityAsync(1, Today, 3);

        // 13:30 + 50min = 14:20 → overlaps [14:00, 14:30).
        result.Data!.Slots.Should().NotContain("13:30");
        result.Data.Slots.Should().Contain("14:30");
    }

    [Fact]
    public async Task GetAvailability_CancelledAndDeletedAppointments_DoNotBlockSlots()
    {
        SetupAppointments(
            MakeAppointment("10:00", 30, Status.Cancelled),
            MakeAppointment("11:00", 30, Status.Deleted));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Data!.Slots.Should().Contain(["10:00", "11:00"]);
    }

    [Fact]
    public async Task GetAvailability_AppointmentsOnOtherDates_DoNotBlockSlots()
    {
        var otherDay = MakeAppointment("10:00", 30);
        otherDay.ScheduledFor = new DateTime(2026, 7, 11, 10, 0, 0);
        SetupAppointments(otherDay);

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Data!.Slots.Should().Contain("10:00");
    }

    // =========================
    // LEAD TIME / PAST DATES
    // =========================

    [Fact]
    public async Task GetAvailability_Today_ExcludesSlotsWithinLeadTime()
    {
        // Shop clock at 14:03 → 10-min lead time → first bookable slot 14:30.
        _clock.Setup(c => c.Now).Returns(new DateTime(2026, 7, 10, 14, 3, 0));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        var slots = result.Data!.Slots;
        slots.Should().NotContain(["13:30", "14:00"]);
        slots.Should().Contain("14:30");
    }

    [Fact]
    public async Task GetAvailability_PastDate_ReturnsNoSlots()
    {
        _clock.Setup(c => c.Now).Returns(new DateTime(2026, 7, 12, 8, 0, 0));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Success.Should().BeTrue();
        result.Data!.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailability_FutureDate_IgnoresLeadTime()
    {
        // Clock on 2026-07-08; requested date is 2026-07-10 → full grid.
        _clock.Setup(c => c.Now).Returns(new DateTime(2026, 7, 8, 17, 55, 0));

        var result = await _sut.GetAvailabilityAsync(1, Today, 1);

        result.Data!.Slots.Should().HaveCount(16);
    }
}
