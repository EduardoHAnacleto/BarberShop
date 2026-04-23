using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class WorkingHoursServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IBusinessScheduleRepository> _scheduleRepo;
    private readonly Mock<IWorkingHoursRepository> _closureRepo;
    private readonly IMapper _mapper;
    private readonly WorkingHoursService _sut;

    public WorkingHoursServiceTests()
    {
        _scheduleRepo = new Mock<IBusinessScheduleRepository>();
        _closureRepo = new Mock<IWorkingHoursRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.BusinessSchedules).Returns(_scheduleRepo.Object);
        _uow.Setup(u => u.WorkingHours).Returns(_closureRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new WorkingHoursService(_uow.Object, _mapper);
    }

    // =========================
    // HELPERS
    // =========================
    private static BusinessSchedule MakeSchedule(
        DayOfWeek day = DayOfWeek.Monday,
        bool isOpen = true,
        TimeSpan? open = null,
        TimeSpan? close = null,
        TimeSpan? brkStart = null,
        TimeSpan? brkEnd = null) => new()
        {
            Id = (int)day + 1,
            DayOfWeek = day,
            IsOpen = isOpen,
            OpenTime = open ?? new TimeSpan(9, 0, 0),
            CloseTime = close ?? new TimeSpan(18, 0, 0),
            BreakStart = brkStart,
            BreakEnd = brkEnd
        };

    private static WorkingHours MakeClosure(
        int id = 1,
        DateTime? closedFrom = null,
        DateTime? closedUntil = null,
        ClosureType type = ClosureType.UntilSpecificDate,
        string reason = "Holiday") => new()
        {
            Id = id,
            ClosedFrom = closedFrom ?? DateTime.UtcNow.AddHours(-1),
            ClosedUntil = closedUntil ?? DateTime.UtcNow.AddHours(8),
            ClosureType = type,
            Reason = reason
        };

    // =========================
    // GET SCHEDULE
    // =========================

    [Fact]
    public async Task GetScheduleAsync_WhenSchedulesExist_ReturnsMappedList()
    {
        // Arrange
        var schedules = new List<BusinessSchedule>
        {
            MakeSchedule(DayOfWeek.Monday),
            MakeSchedule(DayOfWeek.Tuesday)
        };

        _scheduleRepo
            .Setup(r => r.GetAllOrderedAsync())
            .ReturnsAsync(schedules);

        // Act
        var result = await _sut.GetScheduleAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        result[1].DayOfWeek.Should().Be(DayOfWeek.Tuesday);
    }

    [Fact]
    public async Task GetScheduleAsync_WhenNoSchedules_ReturnsEmptyList()
    {
        // Arrange
        _scheduleRepo
            .Setup(r => r.GetAllOrderedAsync())
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetScheduleAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // =========================
    // GET SCHEDULE BY DAY
    // =========================

    [Fact]
    public async Task GetScheduleByDayAsync_WhenDayExists_ReturnsMappedDto()
    {
        // Arrange
        var schedule = MakeSchedule(DayOfWeek.Monday);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        // Act
        var result = await _sut.GetScheduleByDayAsync(DayOfWeek.Monday);

        // Assert
        result.Should().NotBeNull();
        result!.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.IsOpen.Should().BeTrue();
        result.OpenTime.Should().Be(new TimeSpan(9, 0, 0));
        result.CloseTime.Should().Be(new TimeSpan(18, 0, 0));
    }

    [Fact]
    public async Task GetScheduleByDayAsync_WhenDayNotFound_ReturnsNull()
    {
        // Arrange
        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Sunday))
            .ReturnsAsync((BusinessSchedule?)null);

        // Act
        var result = await _sut.GetScheduleByDayAsync(DayOfWeek.Sunday);

        // Assert
        result.Should().BeNull();
    }

    // =========================
    // UPDATE SCHEDULE
    // =========================

    [Fact]
    public async Task UpdateScheduleAsync_WhenScheduleExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var existing = MakeSchedule(DayOfWeek.Monday);

        var dto = new BusinessScheduleDTO
        {
            DayOfWeek = DayOfWeek.Monday,
            IsOpen = false,
            OpenTime = new TimeSpan(10, 0, 0),
            CloseTime = new TimeSpan(17, 0, 0)
        };

        _scheduleRepo
            .Setup(r => r.GetByIdAsync(
                existing.Id,
                It.IsAny<System.Linq.Expressions.Expression<Func<BusinessSchedule, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.UpdateScheduleAsync(existing.Id, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsOpen.Should().BeFalse();
        result.Data.OpenTime.Should().Be(new TimeSpan(10, 0, 0));
        result.Data.CloseTime.Should().Be(new TimeSpan(17, 0, 0));

        _scheduleRepo.Verify(r =>
            r.Update(
                It.Is<BusinessSchedule>(s => !s.IsOpen),
                It.IsAny<System.Linq.Expressions.Expression<Func<BusinessSchedule, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateScheduleAsync_WhenScheduleNotFound_ReturnsFail()
    {
        // Arrange
        _scheduleRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<BusinessSchedule, object>>[]>()))
            .ReturnsAsync((BusinessSchedule?)null);

        // Act
        var result = await _sut.UpdateScheduleAsync(99, new BusinessScheduleDTO());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Schedule not found");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // GET CLOSURES
    // =========================

    [Fact]
    public async Task GetClosuresAsync_WhenClosuresExist_ReturnsList()
    {
        // Arrange
        var closures = new List<WorkingHours>
        {
            MakeClosure(1, reason: "Holiday"),
            MakeClosure(2, reason: "Maintenance")
        };

        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync(closures);

        // Act
        var result = await _sut.GetClosuresAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Reason.Should().Be("Holiday");
        result[1].Reason.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetClosuresAsync_WhenNoClosures_ReturnsEmptyList()
    {
        // Arrange
        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetClosuresAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // =========================
    // ADD CLOSURE
    // =========================

    [Fact]
    public async Task AddClosureAsync_WithValidClosure_ReturnsSuccess()
    {
        // Arrange
        var closure = MakeClosure(reason: "Holiday");

        _closureRepo
            .Setup(r => r.AddAsync(
                It.IsAny<WorkingHours>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync((WorkingHours w,
                System.Linq.Expressions.Expression<Func<WorkingHours, object>>[] _) => w);

        // Act
        var result = await _sut.AddClosureAsync(closure);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Reason.Should().Be("Holiday");

        _closureRepo.Verify(r =>
            r.AddAsync(
                It.Is<WorkingHours>(w => w.Reason == "Holiday"),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // REMOVE CLOSURE
    // =========================

    [Fact]
    public async Task RemoveClosureAsync_WhenClosureExists_ReturnsTrueAndDeletes()
    {
        // Arrange
        var closure = MakeClosure(1);

        _closureRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync(closure);

        // Act
        var result = await _sut.RemoveClosureAsync(1);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();

        _closureRepo.Verify(r =>
            r.Delete(
                It.Is<WorkingHours>(w => w.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveClosureAsync_WhenClosureNotFound_ReturnsFalse()
    {
        // Arrange
        _closureRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync((WorkingHours?)null);

        // Act
        var result = await _sut.RemoveClosureAsync(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();

        _closureRepo.Verify(r =>
            r.Delete(
                It.IsAny<WorkingHours>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // IS OPEN
    // =========================

    [Fact]
    public async Task IsOpenAsync_WhenNoScheduleForDay_ReturnsFalse()
    {
        // Arrange — domingo sem schedule cadastrado
        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Sunday))
            .ReturnsAsync((BusinessSchedule?)null);

        var dateTime = new DateTime(2024, 1, 7, 10, 0, 0); // domingo às 10h

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenDayIsClosedInSchedule_ReturnsFalse()
    {
        // Arrange — domingo com IsOpen = false
        var schedule = MakeSchedule(DayOfWeek.Sunday, isOpen: false);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Sunday))
            .ReturnsAsync(schedule);

        var dateTime = new DateTime(2024, 1, 7, 10, 0, 0);

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenWithinBusinessHoursAndNoClosures_ReturnsTrue()
    {
        // Arrange — segunda aberta, das 9h às 18h, sem fechamento
        var schedule = MakeSchedule(DayOfWeek.Monday);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync([]);

        var dateTime = new DateTime(2024, 1, 8, 10, 0, 0); // segunda às 10h

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOpenAsync_WhenBeforeOpenTime_ReturnsFalse()
    {
        // Arrange
        var schedule = MakeSchedule(DayOfWeek.Monday,
            open: new TimeSpan(9, 0, 0),
            close: new TimeSpan(18, 0, 0));

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        var dateTime = new DateTime(2024, 1, 8, 8, 0, 0); // segunda às 8h — antes de abrir

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenAfterCloseTime_ReturnsFalse()
    {
        // Arrange
        var schedule = MakeSchedule(DayOfWeek.Monday,
            open: new TimeSpan(9, 0, 0),
            close: new TimeSpan(18, 0, 0));

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        var dateTime = new DateTime(2024, 1, 8, 19, 0, 0); // segunda às 19h — depois de fechar

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenDuringBreak_ReturnsFalse()
    {
        // Arrange — segunda com almoço das 12h às 13h
        var schedule = MakeSchedule(DayOfWeek.Monday,
            brkStart: new TimeSpan(12, 0, 0),
            brkEnd: new TimeSpan(13, 0, 0));

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        var dateTime = new DateTime(2024, 1, 8, 12, 30, 0); // segunda às 12h30 — no almoço

        // Act
        var result = await _sut.IsOpenAsync(dateTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenWithinActiveClosure_ReturnsFalse()
    {
        // Arrange — schedule aberto mas há fechamento excepcional ativo
        var schedule = MakeSchedule(DayOfWeek.Monday);
        var now = new DateTime(2024, 1, 8, 10, 0, 0);

        var closure = MakeClosure(
            closedFrom: now.AddHours(-1),
            closedUntil: now.AddHours(4),
            type: ClosureType.UntilSpecificDate);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync([closure]);

        // Act
        var result = await _sut.IsOpenAsync(now);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOpenAsync_WhenClosureExpired_ReturnsTrue()
    {
        // Arrange — fechamento já passou
        var schedule = MakeSchedule(DayOfWeek.Monday);
        var now = new DateTime(2024, 1, 8, 10, 0, 0);

        var closure = MakeClosure(
            closedFrom: now.AddHours(-5),
            closedUntil: now.AddHours(-1), // fechamento terminou 1h atrás
            type: ClosureType.UntilSpecificDate);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync([closure]);

        // Act
        var result = await _sut.IsOpenAsync(now);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOpenAsync_WhenClosureTypeIsUntilNextOpening_ReturnsFalse()
    {
        // Arrange — fechamento até a próxima abertura
        // Usamos terça às 8h como ponto de verificação
        // A próxima abertura após domingo é segunda às 9h
        // Como terça 8h > segunda 9h, o closure já expirou — isso não serve
        // Devemos testar com segunda às 8h (antes da abertura às 9h)
        var monday = new DateTime(2024, 1, 8, 8, 0, 0); // segunda às 8h — antes de abrir
        var schedule = MakeSchedule(DayOfWeek.Monday);

        var closure = MakeClosure(
            closedFrom: monday.AddDays(-1), // fechou domingo
            type: ClosureType.UntilNextOpening);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Monday))
            .ReturnsAsync(schedule);

        _scheduleRepo
            .Setup(r => r.GetByDayAsync(DayOfWeek.Tuesday))
            .ReturnsAsync(MakeSchedule(DayOfWeek.Tuesday,
                open: new TimeSpan(9, 0, 0)));

        _closureRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, bool>>>(),
                It.IsAny<Func<IQueryable<WorkingHours>, IOrderedQueryable<WorkingHours>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<WorkingHours, object>>[]>()))
            .ReturnsAsync([closure]);

        // Act
        var result = await _sut.IsOpenAsync(monday);

        // Assert
        result.Should().BeFalse();
    }
}