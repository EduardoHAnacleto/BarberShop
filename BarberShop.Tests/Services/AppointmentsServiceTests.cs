using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class AppointmentsServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IServiceRepository> _serviceRepo;
    private readonly Mock<IWorkerRepository> _workerRepo;
    private readonly Mock<ICustomerRepository> _customerRepo;
    private readonly Mock<IRedisService> _redis;
    private readonly Mock<INotificationPublisher> _notifications;
    private readonly Mock<IWaitlistService> _waitlist;
    private readonly IMapper _mapper;
    private readonly AppointmentsService _sut;

    public AppointmentsServiceTests()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();

        // Conflict validation resolves the requested service's duration and
        // the worker's active appointments; default to a known 30-min service
        // and a free calendar so each test only overrides what it exercises.
        _serviceRepo = new Mock<IServiceRepository>();
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(new Service { Id = 1, Name = "Haircut", Duration = 30, Price = 35 });
        _appointmentRepo
            .Setup(r => r.GetByWorker(It.IsAny<int>()))
            .ReturnsAsync([]);

        // CreateRecurring resolves worker/customer once up front (for
        // validation and to attach to every occurrence it builds).
        _workerRepo = new Mock<IWorkerRepository>();
        _workerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(new Worker { Id = 1, Name = "John Barber" });

        _customerRepo = new Mock<ICustomerRepository>();
        _customerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(new Customer { Id = 1, Name = "Jane Doe" });

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);
        _uow.Setup(u => u.Services).Returns(_serviceRepo.Object);
        _uow.Setup(u => u.Workers).Returns(_workerRepo.Object);
        _uow.Setup(u => u.Customers).Returns(_customerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _redis = new Mock<IRedisService>();
        _redis.Setup(r => r.GetAsync<List<AppointmentResponseDTO>>(It.IsAny<string>()))
            .ReturnsAsync((List<AppointmentResponseDTO>?)null);
        _redis.Setup(r => r.GetAsync<AppointmentResponseDTO>(It.IsAny<string>()))
            .ReturnsAsync((AppointmentResponseDTO?)null);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<List<AppointmentResponseDTO>>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<AppointmentResponseDTO>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.InvalidateByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _notifications = new Mock<INotificationPublisher>();
        _notifications.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _waitlist = new Mock<IWaitlistService>();
        _waitlist.Setup(w => w.NotifyWaitlistForAsync(It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new AppointmentsService(
            _uow.Object, _mapper, _redis.Object, _notifications.Object,
            NullLogger<AppointmentsService>.Instance, _waitlist.Object);
    }

    // =========================
    // HELPERS
    // =========================
    private static Appointment MakeAppointment(
        int id = 1,
        Status status = Status.Scheduled,
        DateTime? scheduledFor = null) => new()
        {
            Id = id,
            WorkerId = 1,
            CustomerId = 1,
            ServiceId = 1,
            Status = status,
            ScheduledFor = scheduledFor ?? DateTime.UtcNow.AddDays(1),
            Worker = new Worker { Id = 1, Name = "John Barber" },
            Customer = new Customer { Id = 1, Name = "Jane Doe" },
            Service = new Service { Id = 1, Name = "Haircut" }
        };

    private static AppointmentRequestDTO MakeRequestDTO(
        Status status = Status.Scheduled,
        DateTime? scheduledFor = null) => new()
        {
            WorkerId = 1,
            CustomerId = 1,
            ServiceId = 1,
            Status = status,
            ScheduledFor = scheduledFor ?? DateTime.UtcNow.AddDays(1)
        };

    private static RecurringAppointmentRequestDTO MakeRecurringRequestDTO(
        DateTime? scheduledFor = null, int repeatWeeks = 3) => new()
        {
            WorkerId = 1,
            CustomerId = 1,
            ServiceId = 1,
            ScheduledFor = scheduledFor ?? DateTime.Parse("2026-07-10T14:00:00"),
            RepeatWeeks = repeatWeeks,
        };

    // =========================
    // GET ALL
    // =========================

    [Fact]
    public async Task GetAllAsync_WhenAppointmentsExist_ReturnsMappedDtoList()
    {
        // Arrange
        var appointments = new List<Appointment>
        {
            MakeAppointment(1),
            MakeAppointment(2)
        };

        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointments);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].WorkerName.Should().Be("John Barber");
        result[0].CustomerName.Should().Be("Jane Doe");
        result[0].ServiceName.Should().Be("Haircut");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoAppointmentsExist_ReturnsEmptyList()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // =========================
    // GET BY ID
    // =========================

    [Fact]
    public async Task GetByIdAsync_WhenAppointmentExists_ReturnsMappedDto()
    {
        // Arrange
        var appointment = MakeAppointment(1);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.WorkerName.Should().Be("John Barber");
        result.CustomerName.Should().Be("Jane Doe");
        result.ServiceName.Should().Be("Haircut");
        result.Status.Should().Be(Status.Scheduled);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAppointmentNotFound_ReturnsNull()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.GetByIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    // =========================
    // CREATE
    // =========================

    [Fact]
    public async Task Create_WithValidDto_ReturnsSuccessWithMappedDto()
    {
        // Arrange
        var dto = MakeRequestDTO();

        _appointmentRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment a,
                System.Linq.Expressions.Expression<Func<Appointment, object>>[] _) => a);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.WorkerId.Should().Be(1);
        result.Data.CustomerId.Should().Be(1);
        result.Data.ServiceId.Should().Be(1);
        result.Data.Status.Should().Be(Status.Scheduled);

        _appointmentRepo.Verify(r =>
            r.AddAsync(
                It.Is<Appointment>(a => a.WorkerId == 1 && a.CustomerId == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // CREATE — CONFLICT VALIDATION
    // =========================

    // Existing active appointment for the same worker occupying [14:00, 14:30).
    private void SetupExistingAppointment(
        string time = "14:00", int durationMinutes = 30, Status status = Status.Scheduled)
    {
        var existing = MakeAppointment(99, status, DateTime.Parse($"2026-07-10T{time}:00"));
        existing.Service = new Service { Id = 9, Name = "Any", Duration = durationMinutes, Price = 10 };
        _appointmentRepo
            .Setup(r => r.GetByWorker(1))
            .ReturnsAsync([existing]);
    }

    [Fact]
    public async Task Create_WhenSlotOverlapsExistingAppointment_Fails()
    {
        // Arrange — request 30-min service at 14:00, worker already booked 14:00.
        SetupExistingAppointment("14:00", 30);
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T14:00:00"));

        // Act
        var result = await _sut.Create(dto);

        // Assert — the overlapping booking is rejected, nothing persisted.
        result.Success.Should().BeFalse();
        result.Error.Should().Be("This time slot is no longer available");
        _appointmentRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenRequestedDurationRunsIntoExistingAppointment_Fails()
    {
        // Arrange — 50-min service at 13:30 would run to 14:20, overlapping 14:00.
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(new Service { Id = 1, Name = "Combo", Duration = 50, Price = 55 });
        SetupExistingAppointment("14:00", 30);
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T13:30:00"));

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenAdjacentToExistingAppointment_Succeeds()
    {
        // Arrange — existing [14:00, 14:30); new 30-min booking at 14:30 is fine.
        SetupExistingAppointment("14:00", 30);
        _appointmentRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment a,
                System.Linq.Expressions.Expression<Func<Appointment, object>>[] _) => a);
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T14:30:00"));

        // Act
        var result = await _sut.Create(dto);

        // Assert — touching boundaries do not overlap.
        result.Success.Should().BeTrue();
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Create_WhenCancelledAppointmentOccupiesSlot_Succeeds()
    {
        // Arrange — a cancelled booking must not block the slot.
        SetupExistingAppointment("14:00", 30, Status.Cancelled);
        _appointmentRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment a,
                System.Linq.Expressions.Expression<Func<Appointment, object>>[] _) => a);
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T14:00:00"));

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // CREATE RECURRING
    // =========================

    [Fact]
    public async Task CreateRecurring_WithValidDto_CreatesAllOccurrencesWithSharedRecurrenceId()
    {
        // Arrange
        var dto = MakeRecurringRequestDTO(repeatWeeks: 3);
        _appointmentRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment a,
                System.Linq.Expressions.Expression<Func<Appointment, object>>[] _) => a);

        // Act
        var result = await _sut.CreateRecurring(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Created.Should().HaveCount(3);
        result.Data.SkippedDates.Should().BeEmpty();
        result.Data.RecurrenceId.Should().NotBe(Guid.Empty);

        var expectedDates = new[] { 0, 7, 14 }.Select(d => dto.ScheduledFor.AddDays(d));
        result.Data.Created.Select(a => a.ScheduledFor).Should().BeEquivalentTo(expectedDates);

        _appointmentRepo.Verify(r =>
            r.AddAsync(
                It.Is<Appointment>(a => a.RecurrenceId == result.Data.RecurrenceId),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Exactly(3));
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateRecurring_SkipsOccurrencesThatConflict()
    {
        // Arrange — an existing appointment occupies the 2nd occurrence (week +7).
        var dto = MakeRecurringRequestDTO(repeatWeeks: 3);
        var conflictingDate = dto.ScheduledFor.AddDays(7);
        var existing = MakeAppointment(99, Status.Scheduled, conflictingDate);
        existing.Service = new Service { Id = 9, Name = "Any", Duration = 30, Price = 10 };
        _appointmentRepo.Setup(r => r.GetByWorker(1)).ReturnsAsync([existing]);
        _appointmentRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment a,
                System.Linq.Expressions.Expression<Func<Appointment, object>>[] _) => a);

        // Act
        var result = await _sut.CreateRecurring(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Created.Should().HaveCount(2);
        result.Data.SkippedDates.Should().ContainSingle().Which.Should().Be(conflictingDate);
    }

    [Fact]
    public async Task CreateRecurring_WhenAllOccurrencesConflict_ReturnsFail()
    {
        // Arrange — a weekly recurring conflict (e.g. another standing booking)
        // blocks every single occurrence. Duration must be explicit — the
        // MakeAppointment default Service has Duration 0 (zero-width, never
        // overlaps), same caveat as SetupExistingAppointment above.
        var dto = MakeRecurringRequestDTO(repeatWeeks: 2);
        var blocker1 = MakeAppointment(97, Status.Scheduled, dto.ScheduledFor);
        blocker1.Service = new Service { Id = 9, Name = "Any", Duration = 30, Price = 10 };
        var blocker2 = MakeAppointment(98, Status.Scheduled, dto.ScheduledFor.AddDays(7));
        blocker2.Service = new Service { Id = 9, Name = "Any", Duration = 30, Price = 10 };
        _appointmentRepo.Setup(r => r.GetByWorker(1)).ReturnsAsync([blocker1, blocker2]);

        // Act
        var result = await _sut.CreateRecurring(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("All occurrences conflict with existing appointments");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task CreateRecurring_WhenRepeatWeeksOutOfRange_ReturnsFail(int repeatWeeks)
    {
        var dto = MakeRecurringRequestDTO(repeatWeeks: repeatWeeks);

        var result = await _sut.CreateRecurring(dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("RepeatWeeks must be between 1 and 12");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateRecurring_WhenWorkerNotFound_ReturnsFail()
    {
        _workerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);
        var dto = MakeRecurringRequestDTO();

        var result = await _sut.CreateRecurring(dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Worker not found");
    }

    [Fact]
    public async Task CreateRecurring_WhenCustomerNotFound_ReturnsFail()
    {
        _customerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer?)null);
        var dto = MakeRecurringRequestDTO();

        var result = await _sut.CreateRecurring(dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Customer not found");
    }

    [Fact]
    public async Task CreateRecurring_WhenServiceNotFound_ReturnsFail()
    {
        _serviceRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);
        var dto = MakeRecurringRequestDTO();

        var result = await _sut.CreateRecurring(dto);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Service not found");
    }

    // =========================
    // CHANGE STATUS (worker actions: start / complete / no-show)
    // =========================

    [Fact]
    public async Task ChangeStatus_ToCompleted_SetsCompletedAtAndPersists()
    {
        // Arrange
        var entity = MakeAppointment(1, Status.OnGoing);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _sut.ChangeStatus(1, Status.Completed);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(Status.Completed);
        entity.CompletedAt.Should().NotBeNull();
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_ToOnGoing_DoesNotSetCompletedAt()
    {
        var entity = MakeAppointment(1, Status.Scheduled);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        var result = await _sut.ChangeStatus(1, Status.OnGoing);

        result.Data!.Status.Should().Be(Status.OnGoing);
        entity.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task ChangeStatus_WhenNotFound_ReturnsNullData()
    {
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _sut.ChangeStatus(99, Status.Completed);

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // UPDATE
    // =========================

    [Fact]
    public async Task Update_WhenAppointmentExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var entity = MakeAppointment(1, Status.Scheduled);
        var dto = MakeRequestDTO(Status.OnGoing);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(Status.OnGoing);

        _appointmentRepo.Verify(r =>
            r.Update(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_RescheduleIntoOccupiedSlot_Fails()
    {
        // Arrange — appointment #1 being moved onto #99's slot at 14:00.
        var entity = MakeAppointment(1, Status.Scheduled);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);
        SetupExistingAppointment("14:00", 30); // sets GetByWorker to [#99]
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T14:00:00"));

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("This time slot is no longer available");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_RescheduleKeepingOwnSlot_Succeeds()
    {
        // Arrange — the appointment's own existing slot must not count as a
        // conflict against itself when only, say, the notes change.
        var entity = MakeAppointment(50, Status.Scheduled,
            DateTime.Parse("2026-07-10T14:00:00"));
        entity.Service = new Service { Id = 1, Name = "Haircut", Duration = 30, Price = 35 };
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                50,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);
        _appointmentRepo
            .Setup(r => r.GetByWorker(1))
            .ReturnsAsync([entity]); // only itself on the calendar
        var dto = MakeRequestDTO(scheduledFor: DateTime.Parse("2026-07-10T14:00:00"));

        // Act
        var result = await _sut.Update(50, dto);

        // Assert
        result.Success.Should().BeTrue();
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenStatusChangesToCompleted_SetsCompletedAt()
    {
        // Arrange
        var entity = MakeAppointment(1, Status.OnGoing);
        entity.CompletedAt = null;
        var dto = MakeRequestDTO(Status.Completed);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        await _sut.Update(1, dto);

        // Assert
        entity.CompletedAt.Should().NotBeNull();
        entity.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_WhenAlreadyCompleted_DoesNotOverwriteCompletedAt()
    {
        // Arrange
        var completedAt = DateTime.UtcNow.AddHours(-1);
        var entity = MakeAppointment(1, Status.Completed);
        entity.CompletedAt = completedAt;
        var dto = MakeRequestDTO(Status.Completed);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        await _sut.Update(1, dto);

        // Assert
        entity.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task Update_WhenAppointmentNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.Update(99, MakeRequestDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _appointmentRepo.Verify(r =>
            r.Update(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenAppointmentNotFound_ReturnsFail()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.Delete(99);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Appointment not found");
    }

    [Theory]
    [InlineData(Status.Cancelled, "Already cancelled")]
    [InlineData(Status.Completed, "Completed cannot be cancelled")]
    [InlineData(Status.Deleted, "Already deleted")]
    public async Task Delete_WhenAppointmentHasInvalidStatus_ReturnsFail(
        Status status, string expectedError)
    {
        // Arrange
        var entity = MakeAppointment(1, status);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);

        _appointmentRepo.Verify(r =>
            r.VirtualDelete(It.IsAny<Appointment>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Theory]
    [InlineData(Status.Scheduled)]
    [InlineData(Status.OnGoing)]
    public async Task Delete_WhenAppointmentIsCancellable_CancelsSuccessfully(Status status)
    {
        // Arrange
        var entity = MakeAppointment(1, status);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        _appointmentRepo
            .Setup(r => r.VirtualDelete(It.IsAny<Appointment>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeTrue();

        _appointmentRepo.Verify(r =>
            r.VirtualDelete(It.Is<Appointment>(a => a.Id == 1)),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenCancelled_NotifiesTheWaitlistForThatWorkerAndDay()
    {
        var scheduledFor = DateTime.Parse("2026-08-01T14:00:00");
        var entity = MakeAppointment(1, Status.Scheduled, scheduledFor);
        entity.WorkerId = 7;

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);
        _appointmentRepo.Setup(r => r.VirtualDelete(It.IsAny<Appointment>())).Returns(Task.CompletedTask);

        await _sut.Delete(1);

        _waitlist.Verify(w => w.NotifyWaitlistForAsync(7, scheduledFor), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenWaitlistNotificationThrows_StillReturnsSuccess()
    {
        var entity = MakeAppointment(1, Status.Scheduled);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);
        _appointmentRepo.Setup(r => r.VirtualDelete(It.IsAny<Appointment>())).Returns(Task.CompletedTask);
        _waitlist.Setup(w => w.NotifyWaitlistForAsync(It.IsAny<int>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await _sut.Delete(1);

        // The cancellation already committed — a notification hiccup must not
        // surface as a failed cancel.
        result.Success.Should().BeTrue();
    }

    // =========================
    // FILTERS
    // =========================

    [Fact]
    public async Task GetByDateRange_ReturnsAppointmentsWithinRange()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([MakeAppointment(1)]);

        // Act
        var result = await _sut.GetByDateRange(
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1));

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByWorker_ReturnsAppointmentsForWorker()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([MakeAppointment(1), MakeAppointment(2)]);

        // Act
        var result = await _sut.GetByWorker(1);

        // Assert
        result.Should().HaveCount(2);
        result.All(r => r.WorkerId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetByCustomer_ReturnsAppointmentsForCustomer()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([MakeAppointment(1)]);

        // Act
        var result = await _sut.GetByCustomer(1);

        // Assert
        result.Should().HaveCount(1);
        result[0].CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task GetByService_ReturnsAppointmentsForService()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([MakeAppointment(1)]);

        // Act
        var result = await _sut.GetByService(1);

        // Assert
        result.Should().HaveCount(1);
        result[0].ServiceId.Should().Be(1);
    }

    [Fact]
    public async Task GetByStatus_ReturnsAppointmentsWithMatchingStatus()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<Func<IQueryable<Appointment>, IOrderedQueryable<Appointment>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync([MakeAppointment(1, Status.Scheduled), MakeAppointment(2, Status.Scheduled)]);

        // Act
        var result = await _sut.GetByStatus(Status.Scheduled);

        // Assert
        result.Should().HaveCount(2);
        result.All(r => r.Status == Status.Scheduled).Should().BeTrue();
    }

    // =========================
    // DELAY APPOINTMENTS
    // =========================

    [Fact]
    public async Task DelayAppointments_WithEmptyList_ReturnsFail()
    {
        // Act
        var result = await _sut.DelayAppointments([], TimeSpan.FromHours(1));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("No appointments provided");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task DelayAppointments_WithZeroTime_ReturnsFail()
    {
        // Act
        var result = await _sut.DelayAppointments([1], TimeSpan.Zero);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Delay time must be greater than zero");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task DelayAppointments_WithValidIds_ReturnsSuccessWithUpdatedAppointments()
    {
        // Arrange
        var scheduledFor = DateTime.UtcNow.AddDays(1);
        var entity = MakeAppointment(1, scheduledFor: scheduledFor);
        var delay = TimeSpan.FromHours(2);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _sut.DelayAppointments([1], delay);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        entity.ScheduledFor.Should().Be(scheduledFor.Add(delay));
        entity.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _appointmentRepo.Verify(r =>
            r.Update(
                It.Is<Appointment>(a => a.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task DelayAppointments_WhenIdNotFound_SkipsAndReturnsOnlyFound()
    {
        // Arrange
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.DelayAppointments([99], TimeSpan.FromHours(1));

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();

        _appointmentRepo.Verify(r =>
            r.Update(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Never);
    }

    [Fact]
    public async Task DelayAppointments_WithMultipleIds_DelaysAllFound()
    {
        // Arrange
        var entity1 = MakeAppointment(1);
        var entity2 = MakeAppointment(2);
        var delay = TimeSpan.FromMinutes(30);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity1);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(2,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity2);

        // Act
        var result = await _sut.DelayAppointments([1, 2], delay);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);

        _appointmentRepo.Verify(r =>
            r.Update(
                It.IsAny<Appointment>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Exactly(2));

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // CANCEL APPOINTMENTS
    // =========================

    [Fact]
    public async Task CancelAppointments_WithEmptyList_ReturnsFail()
    {
        // Act
        var result = await _sut.CancelAppointments([]);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("No appointments provided");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelAppointments_WithValidIds_ReturnsSuccessWithCancelledList()
    {
        // Arrange
        var entity1 = MakeAppointment(1);
        var entity2 = MakeAppointment(2);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity1);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(2,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity2);

        // Act
        var result = await _sut.CancelAppointments([1, 2]);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].Status.Should().Be(Status.Cancelled);
        result.Data![1].Status.Should().Be(Status.Cancelled);

        _appointmentRepo.Verify(r =>
            r.Update(
                It.Is<Appointment>(a => a.Status == Status.Cancelled),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Exactly(2));

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAppointments_NotifiesTheWaitlistForEachFreedWorker()
    {
        var entity = MakeAppointment(1);
        entity.WorkerId = 7;
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        await _sut.CancelAppointments([1]);

        _waitlist.Verify(w => w.NotifyWaitlistForAsync(7, entity.ScheduledFor), Times.Once);
    }

    [Fact]
    public async Task CancelAppointments_WhenIdNotFound_SkipsAndReturnsOnlyFound()
    {
        // Arrange
        var entity = MakeAppointment(1);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(entity);

        _appointmentRepo
            .Setup(r => r.GetByIdAsync(99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _sut.CancelAppointments([1, 99]);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Status.Should().Be(Status.Cancelled);

        _appointmentRepo.Verify(r =>
            r.Update(
                It.Is<Appointment>(a => a.Status == Status.Cancelled),
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }
}