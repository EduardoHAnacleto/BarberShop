using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace BarberShop.Tests.Services;

public class AppointmentsServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IHubContext<AppointmentsHub>> _hub;
    private readonly IMapper _mapper;
    private readonly AppointmentsService _sut;

    public AppointmentsServiceTests()
    {
        _appointmentRepo = new Mock<IAppointmentRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _hub = new Mock<IHubContext<AppointmentsHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClient = new Mock<IClientProxy>();
        _hub.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.All).Returns(mockClient.Object);
        mockClient
            .Setup(c => c.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new AppointmentsService(_uow.Object, _mapper, BuildRedis(), _hub.Object);
    }

    private static RedisService BuildRedis()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        var server = new Mock<IServer>();

        database
            .Setup(d => d.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        database
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        database
            .Setup(d => d.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        multiplexer
            .Setup(m => m.GetEndPoints(It.IsAny<bool>()))
            .Returns([new System.Net.DnsEndPoint("localhost", 6379)]);

        multiplexer
            .Setup(m => m.GetServer(
                It.IsAny<System.Net.EndPoint>(),
                It.IsAny<object>()))
            .Returns(server.Object);

        server
            .Setup(s => s.Keys(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Returns([]);

        multiplexer
            .Setup(m => m.GetDatabase(
                It.IsAny<int>(),
                It.IsAny<object>()))
            .Returns(database.Object);

        return new RedisService(multiplexer.Object);
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

        _appointmentRepo
            .Setup(r => r.VirtualDelete(It.IsAny<Appointment>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CancelAppointments([1, 2]);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);

        _appointmentRepo.Verify(r =>
            r.VirtualDelete(It.IsAny<Appointment>()),
            Times.Exactly(2));

        _uow.Verify(u => u.SaveAsync(), Times.Once);
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

        _appointmentRepo
            .Setup(r => r.VirtualDelete(It.IsAny<Appointment>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CancelAppointments([1, 99]);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);

        _appointmentRepo.Verify(r =>
            r.VirtualDelete(It.IsAny<Appointment>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }
}