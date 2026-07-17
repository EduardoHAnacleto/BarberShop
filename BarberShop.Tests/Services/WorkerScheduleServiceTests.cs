using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace BarberShop.Tests.Services;

public class WorkerScheduleServiceTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWorkerScheduleRepository> _scheduleRepo;
    private readonly Mock<IWorkerRepository> _workerRepo;
    private readonly Mock<IRedisService> _redis;
    private readonly Mock<INotificationPublisher> _notifications;
    private readonly IMapper _mapper;
    private readonly WorkerScheduleService _sut;

    public WorkerScheduleServiceTests()
    {
        _scheduleRepo = new Mock<IWorkerScheduleRepository>();
        _workerRepo = new Mock<IWorkerRepository>();

        _workerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(new Worker { Id = 1, Name = "James Carter" });

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.WorkerSchedules).Returns(_scheduleRepo.Object);
        _uow.Setup(u => u.Workers).Returns(_workerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _redis = new Mock<IRedisService>();
        _redis.Setup(r => r.InvalidateByPrefixAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _notifications = new Mock<INotificationPublisher>();
        _notifications.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new WorkerScheduleService(
            _uow.Object, _mapper, _redis.Object, _notifications.Object, NullLogger<WorkerScheduleService>.Instance);
    }

    private static WorkerSchedule MakeOverride(
        int workerId = 1,
        DayOfWeek day = DayOfWeek.Monday,
        bool isOpen = true,
        TimeSpan? open = null,
        TimeSpan? close = null) => new()
        {
            Id = 5,
            WorkerId = workerId,
            DayOfWeek = day,
            IsOpen = isOpen,
            OpenTime = open ?? new TimeSpan(10, 0, 0),
            CloseTime = close ?? new TimeSpan(16, 0, 0),
        };

    private static WorkerScheduleDTO MakeDto(
        bool isOpen = true,
        TimeSpan? open = null,
        TimeSpan? close = null) => new()
        {
            IsOpen = isOpen,
            OpenTime = isOpen ? open ?? new TimeSpan(10, 0, 0) : null,
            CloseTime = isOpen ? close ?? new TimeSpan(16, 0, 0) : null,
        };

    // =========================
    // GET BY WORKER
    // =========================

    [Fact]
    public async Task GetByWorkerAsync_ReturnsMappedOverrides()
    {
        _scheduleRepo
            .Setup(r => r.GetByWorkerAsync(1))
            .ReturnsAsync([MakeOverride(day: DayOfWeek.Tuesday)]);

        var result = await _sut.GetByWorkerAsync(1);

        result.Should().HaveCount(1);
        result[0].DayOfWeek.Should().Be(DayOfWeek.Tuesday);
        result[0].WorkerId.Should().Be(1);
    }

    // =========================
    // UPSERT
    // =========================

    [Fact]
    public async Task UpsertAsync_WhenWorkerNotFound_ReturnsFail()
    {
        var result = await _sut.UpsertAsync(99, DayOfWeek.Monday, MakeDto());

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Worker not found");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_WhenOpenWithoutTimes_ReturnsFail()
    {
        var dto = new WorkerScheduleDTO { IsOpen = true, OpenTime = null, CloseTime = null };

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("OpenTime and CloseTime are required");
    }

    [Fact]
    public async Task UpsertAsync_WhenCloseBeforeOpen_ReturnsFail()
    {
        var dto = MakeDto(open: new TimeSpan(16, 0, 0), close: new TimeSpan(10, 0, 0));

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("CloseTime must be after OpenTime");
    }

    [Fact]
    public async Task UpsertAsync_WhenBreakEndNotAfterBreakStart_ReturnsFail()
    {
        var dto = MakeDto(); // 10:00–16:00
        dto.BreakStart = new TimeSpan(14, 0, 0);
        dto.BreakEnd = new TimeSpan(12, 0, 0);

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("BreakEnd must be after BreakStart");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_WhenBreakOutsideOpenHours_ReturnsFail()
    {
        var dto = MakeDto(); // 10:00–16:00
        dto.BreakStart = new TimeSpan(9, 0, 0); // starts before opening
        dto.BreakEnd = new TimeSpan(11, 0, 0);

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("within the open hours");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_WhenOnlyOneBreakEndpointProvided_ReturnsFail()
    {
        var dto = MakeDto();
        dto.BreakStart = new TimeSpan(12, 0, 0);
        dto.BreakEnd = null;

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("BreakStart and BreakEnd must be provided together");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_WithValidBreak_PersistsBreak()
    {
        _scheduleRepo
            .Setup(r => r.GetByWorkerAndDayAsync(1, DayOfWeek.Monday))
            .ReturnsAsync((WorkerSchedule?)null);

        var dto = MakeDto(); // 10:00–16:00
        dto.BreakStart = new TimeSpan(12, 0, 0);
        dto.BreakEnd = new TimeSpan(13, 0, 0);

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeTrue();
        result.Data!.BreakStart.Should().Be(new TimeSpan(12, 0, 0));
        result.Data.BreakEnd.Should().Be(new TimeSpan(13, 0, 0));
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_WhenNoExistingOverride_CreatesNewRow()
    {
        _scheduleRepo
            .Setup(r => r.GetByWorkerAndDayAsync(1, DayOfWeek.Monday))
            .ReturnsAsync((WorkerSchedule?)null);

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, MakeDto());

        result.Success.Should().BeTrue();
        result.Data!.IsOpen.Should().BeTrue();
        _scheduleRepo.Verify(r => r.AddAsync(
            It.Is<WorkerSchedule>(s => s.WorkerId == 1 && s.DayOfWeek == DayOfWeek.Monday),
            It.IsAny<Expression<Func<WorkerSchedule, object>>[]>()), Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
        _notifications.Verify(n => n.PublishAsync("worker-schedules", "WorkerSchedulesChanged"), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_WhenExistingOverride_UpdatesInPlace()
    {
        var existing = MakeOverride();
        _scheduleRepo
            .Setup(r => r.GetByWorkerAndDayAsync(1, DayOfWeek.Monday))
            .ReturnsAsync(existing);

        var dto = MakeDto(open: new TimeSpan(8, 0, 0), close: new TimeSpan(12, 0, 0));

        var result = await _sut.UpsertAsync(1, DayOfWeek.Monday, dto);

        result.Success.Should().BeTrue();
        result.Data!.OpenTime.Should().Be(new TimeSpan(8, 0, 0));
        result.Data.CloseTime.Should().Be(new TimeSpan(12, 0, 0));
        _scheduleRepo.Verify(r => r.AddAsync(
            It.IsAny<WorkerSchedule>(),
            It.IsAny<Expression<Func<WorkerSchedule, object>>[]>()), Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
        _notifications.Verify(n => n.PublishAsync("worker-schedules", "WorkerSchedulesChanged"), Times.Once);
    }

    // =========================
    // REMOVE OVERRIDE
    // =========================

    [Fact]
    public async Task RemoveOverrideAsync_WhenExists_DeletesAndReturnsTrue()
    {
        var existing = MakeOverride();
        _scheduleRepo
            .Setup(r => r.GetByWorkerAndDayAsync(1, DayOfWeek.Monday))
            .ReturnsAsync(existing);

        var result = await _sut.RemoveOverrideAsync(1, DayOfWeek.Monday);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        _scheduleRepo.Verify(r => r.Delete(existing, It.IsAny<Expression<Func<WorkerSchedule, object>>[]>()), Times.Once);
        _notifications.Verify(n => n.PublishAsync("worker-schedules", "WorkerSchedulesChanged"), Times.Once);
    }

    [Fact]
    public async Task RemoveOverrideAsync_WhenNotFound_ReturnsFalseWithoutError()
    {
        _scheduleRepo
            .Setup(r => r.GetByWorkerAndDayAsync(1, DayOfWeek.Monday))
            .ReturnsAsync((WorkerSchedule?)null);

        var result = await _sut.RemoveOverrideAsync(1, DayOfWeek.Monday);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
        _uow.Verify(u => u.SaveAsync(), Times.Never);
        _notifications.Verify(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
