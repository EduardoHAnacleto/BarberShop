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

public class WorkersServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWorkerRepository> _workerRepo;
    private readonly Mock<IServiceRepository> _serviceRepo;
    private readonly Mock<IHubContext<WorkersHub>> _hub;
    private readonly IMapper _mapper;
    private readonly WorkersService _sut;

    public WorkersServiceTests()
    {
        // Repositórios
        _workerRepo = new Mock<IWorkerRepository>();
        _serviceRepo = new Mock<IServiceRepository>();

        // UoW
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Workers).Returns(_workerRepo.Object);
        _uow.Setup(u => u.Services).Returns(_serviceRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        // Hub
        _hub = new Mock<IHubContext<WorkersHub>>();
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

        // AutoMapper versão 16
        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new WorkersService(_uow.Object, _mapper, BuildRedis(), _hub.Object);
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
    private static Worker MakeWorker(int id = 1, string name = "John Doe Barber") => new()
    {
        Id = id,
        Name = name,
        Email = "john@barbershop.com",
        PhoneNumber = "11999999999",
        Address = "123 Main St",
        Position = "Barber",
        WagePerHour = 25.00m,
        DateOfBirth = new DateTime(1990, 1, 1),
        ProvidedServices = new List<Service>()
    };

    private static WorkerDTO MakeWorkerDTO(string name = "John Doe Barber") => new()
    {
        Name = name,
        Email = "john@barbershop.com",
        PhoneNumber = "11999999999",
        Address = "123 Main St",
        Position = "Barber",
        WagePerHour = 25.00m,
        DateOfBirth = new DateTime(1990, 1, 1),
        ServicesId = []
    };

    // =========================
    // GET ALL
    // =========================

    [Fact]
    public async Task GetAllAsync_WhenWorkersExist_ReturnsMappedDtoList()
    {
        // Arrange
        var workers = new List<Worker>
        {
            MakeWorker(1, "John Doe Barber"),
            MakeWorker(2, "Jane Doe Barber")
        };

        _workerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, bool>>>(),
                It.IsAny<Func<IQueryable<Worker>, IOrderedQueryable<Worker>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(workers);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("John Doe Barber");
        result[1].Name.Should().Be("Jane Doe Barber");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoWorkersExist_ReturnsEmptyList()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, bool>>>(),
                It.IsAny<Func<IQueryable<Worker>, IOrderedQueryable<Worker>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
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
    public async Task GetByIdAsync_WhenWorkerExists_ReturnsMappedDto()
    {
        // Arrange
        var worker = MakeWorker(1);

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(worker);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("John Doe Barber");
        result.Email.Should().Be("john@barbershop.com");
        result.WagePerHour.Should().Be(25.00m);
    }

    [Fact]
    public async Task GetByIdAsync_WhenWorkerNotFound_ReturnsNull()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);

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
        var dto = MakeWorkerDTO();

        _workerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker w,
                System.Linq.Expressions.Expression<Func<Worker, object>>[] _) => w);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("John Doe Barber");
        result.Data.WagePerHour.Should().Be(25.00m);

        _workerRepo.Verify(r =>
            r.AddAsync(
                It.Is<Worker>(w => w.Name == "John Doe Barber"),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Create_WithValidDtoAndServices_AddsServicesToWorker()
    {
        // Arrange
        var service1 = new Service { Id = 1, Name = "Haircut", Duration = 30, Price = 25m };
        var service2 = new Service { Id = 2, Name = "Beard Trim", Duration = 20, Price = 15m };

        var dto = MakeWorkerDTO();
        dto.ServicesId = [1, 2];

        _serviceRepo
            .Setup(r => r.GetByIdAsync(1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(service1);

        _serviceRepo
            .Setup(r => r.GetByIdAsync(2,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(service2);

        _workerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker w,
                System.Linq.Expressions.Expression<Func<Worker, object>>[] _) => w);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ProvidedServices.Should().HaveCount(2);
        result.Data.ProvidedServices[0].Name.Should().Be("Haircut");
        result.Data.ProvidedServices[1].Name.Should().Be("Beard Trim");

        _serviceRepo.Verify(r =>
            r.GetByIdAsync(1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Once);

        _serviceRepo.Verify(r =>
            r.GetByIdAsync(2,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_WithNonExistentServiceId_SkipsAndStillSucceeds()
    {
        // Arrange
        var dto = MakeWorkerDTO();
        dto.ServicesId = [99];

        _serviceRepo
            .Setup(r => r.GetByIdAsync(99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);

        _workerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker w,
                System.Linq.Expressions.Expression<Func<Worker, object>>[] _) => w);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ProvidedServices.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, 25.00, "Invalid Name")] // nome nulo
    [InlineData("", 25.00, "Invalid Name")] // nome vazio
    [InlineData("   ", 25.00, "Invalid Name")] // nome só espaços
    [InlineData("Short", 25.00, "Invalid Name")] // menos de 10 chars
    [InlineData("John Doe Barber", 0, "Invalid Wage")] // wage zero
    [InlineData("John Doe Barber", -1, "Invalid Wage")] // wage negativo
    public async Task Create_WithInvalidDto_ReturnsFailWithExpectedError(
        string? name, decimal wage, string expectedError)
    {
        // Arrange
        var dto = MakeWorkerDTO(name!);
        dto.WagePerHour = wage;

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);
        result.Data.Should().BeNull();

        _workerRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // UPDATE
    // =========================

    [Fact]
    public async Task Update_WhenWorkerExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var existing = MakeWorker(1);

        var dto = MakeWorkerDTO("Updated Barber Name");
        dto.WagePerHour = 35.00m;

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Updated Barber Name");
        result.Data.WagePerHour.Should().Be(35.00m);

        _workerRepo.Verify(r =>
            r.Update(
                It.Is<Worker>(w => w.Name == "Updated Barber Name"),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenWorkerExists_SetsLastUpdatedAt()
    {
        // Arrange
        var existing = MakeWorker(1);
        existing.LastUpdatedAt = null;

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, MakeWorkerDTO());

        // Assert
        existing.LastUpdatedAt.Should().NotBeNull();
        existing.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_WhenWorkerNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);

        // Act
        var result = await _sut.Update(99, MakeWorkerDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _workerRepo.Verify(r =>
            r.Update(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenWorkerExists_ReturnsSuccessAndCallsRepository()
    {
        // Arrange
        var worker = MakeWorker(1);

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(worker);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeTrue();

        _workerRepo.Verify(r =>
            r.Delete(
                It.Is<Worker>(w => w.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenWorkerNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);

        // Act
        var result = await _sut.Delete(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _workerRepo.Verify(r =>
            r.Delete(
                It.IsAny<Worker>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // GET SERVICES BY WORKER
    // =========================

    [Fact]
    public async Task GetServicesByWorker_WhenWorkerExists_ReturnsMappedServiceList()
    {
        // Arrange
        var worker = MakeWorker(1);
        worker.ProvidedServices = new List<Service>
        {
            new() { Id = 1, Name = "Haircut",    Duration = 30, Price = 25m },
            new() { Id = 2, Name = "Beard Trim", Duration = 20, Price = 15m }
        };

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(worker);

        // Act
        var result = await _sut.GetServicesByWorker(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Name.Should().Be("Haircut");
        result[1].Name.Should().Be("Beard Trim");
    }

    [Fact]
    public async Task GetServicesByWorker_WhenWorkerNotFound_ReturnsNull()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);

        // Act
        var result = await _sut.GetServicesByWorker(99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetServicesByWorker_WhenWorkerHasNoServices_ReturnsEmptyList()
    {
        // Arrange
        var worker = MakeWorker(1);
        worker.ProvidedServices = new List<Service>();

        _workerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(worker);

        // Act
        var result = await _sut.GetServicesByWorker(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // =========================
    // GET WORKERS BY SERVICE
    // =========================

    [Fact]
    public async Task GetWorkersByService_WhenWorkersExist_ReturnsMappedWorkerList()
    {
        // Arrange
        var workers = new List<Worker>
        {
            MakeWorker(1, "John Doe Barber"),
            MakeWorker(2, "Jane Doe Barber")
        };

        _workerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, bool>>>(),
                It.IsAny<Func<IQueryable<Worker>, IOrderedQueryable<Worker>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(workers);

        // Act
        var result = await _sut.GetWorkersByService(1);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("John Doe Barber");
        result[1].Name.Should().Be("Jane Doe Barber");
    }

    [Fact]
    public async Task GetWorkersByService_WhenNoWorkersProvideService_ReturnsEmptyList()
    {
        // Arrange
        _workerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, bool>>>(),
                It.IsAny<Func<IQueryable<Worker>, IOrderedQueryable<Worker>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetWorkersByService(99);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}