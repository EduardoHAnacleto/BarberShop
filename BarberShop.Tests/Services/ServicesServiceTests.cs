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

public class ServicesServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IServiceRepository> _serviceRepo;
    private readonly Mock<IHubContext<ServicesHub>> _hub;
    private readonly IMapper _mapper;
    private readonly ServicesService _sut;

    public ServicesServiceTests()
    {
        // Repositório e UoW
        _serviceRepo = new Mock<IServiceRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Services).Returns(_serviceRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        // Hub
        _hub = new Mock<IHubContext<ServicesHub>>();
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
            cfg.AddMaps(typeof(MappingProfile).Assembly);
        }, NullLoggerFactory.Instance).CreateMapper();

        // Redis mockado com cache sempre vazio
        var redis = BuildRedis();

        _sut = new ServicesService(_uow.Object, _mapper, redis, _hub.Object);
    }

    private static RedisService BuildRedis()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        var server = new Mock<IServer>();

        // Cache sempre vazio — força o serviço a ir ao repositório
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
    // GET ALL
    // =========================

    [Fact]
    public async Task GetAllAsync_WhenServicesExist_ReturnsMappedDtoList()
    {
        // Arrange
        var services = new List<Service>
        {
            new() { Id = 1, Name = "Haircut",    Duration = 30, Price = 25.00m },
            new() { Id = 2, Name = "Beard Trim", Duration = 20, Price = 15.00m }
        };

        _serviceRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, bool>>>(),
                It.IsAny<Func<IQueryable<Service>, IOrderedQueryable<Service>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(services);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Haircut");
        result[1].Name.Should().Be("Beard Trim");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoServicesExist_ReturnsEmptyList()
    {
        // Arrange
        _serviceRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, bool>>>(),
                It.IsAny<Func<IQueryable<Service>, IOrderedQueryable<Service>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
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
    public async Task GetByIdAsync_WhenServiceExists_ReturnsMappedDto()
    {
        // Arrange
        var service = new Service
        {
            Id = 1,
            Name = "Haircut",
            Description = "Classic haircut",
            Duration = 30,
            Price = 25.00m
        };

        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(service);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("Haircut");
        result.Description.Should().Be("Classic haircut");
        result.Duration.Should().Be(30);
        result.Price.Should().Be(25.00m);
    }

    [Fact]
    public async Task GetByIdAsync_WhenServiceNotFound_ReturnsNull()
    {
        // Arrange
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);

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
        var dto = new ServiceDTO
        {
            Name = "Haircut",
            Description = "Classic haircut",
            Duration = 30,
            Price = 25.00m
        };

        _serviceRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Service>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service s,
                System.Linq.Expressions.Expression<Func<Service, object>>[] _) => s);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Haircut");
        result.Data.Duration.Should().Be(30);
        result.Data.Price.Should().Be(25.00m);

        _serviceRepo.Verify(r =>
            r.AddAsync(
                It.Is<Service>(s => s.Name == "Haircut" && s.Duration == 30),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Theory]
    [InlineData("", 30, 25.00, "Invalid Name")]
    [InlineData("  ", 30, 25.00, "Invalid Name")]
    [InlineData("Ab", 30, 25.00, "Invalid Name")]
    [InlineData("Haircut", 0, 25.00, "Invalid Duration")]
    [InlineData("Haircut", -1, 25.00, "Invalid Duration")]
    [InlineData("Haircut", 30, 0.00, "Invalid Price")]
    [InlineData("Haircut", 30, -5.00, "Invalid Price")]
    public async Task Create_WithInvalidDto_ReturnsFailWithExpectedError(
        string name, int duration, decimal price, string expectedError)
    {
        // Arrange
        var dto = new ServiceDTO { Name = name, Duration = duration, Price = price };

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);
        result.Data.Should().BeNull();

        _serviceRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<Service>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // UPDATE
    // =========================

    [Fact]
    public async Task Update_WhenServiceExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var existing = new Service
        {
            Id = 1,
            Name = "Haircut",
            Duration = 30,
            Price = 25.00m
        };

        var dto = new ServiceDTO
        {
            Name = "Premium Haircut",
            Duration = 45,
            Price = 40.00m
        };

        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Premium Haircut");
        result.Data.Duration.Should().Be(45);
        result.Data.Price.Should().Be(40.00m);

        _serviceRepo.Verify(r =>
            r.Update(
                It.Is<Service>(s => s.Name == "Premium Haircut" && s.Duration == 45),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenServiceExists_SetsLastUpdatedAt()
    {
        // Arrange
        var existing = new Service
        {
            Id = 1,
            Name = "Haircut",
            Duration = 30,
            Price = 25.00m,
            LastUpdatedAt = null
        };

        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, new ServiceDTO { Name = "Haircut", Duration = 30, Price = 25.00m });

        // Assert
        existing.LastUpdatedAt.Should().NotBeNull();
        existing.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_WhenServiceNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);

        // Act
        var result = await _sut.Update(99, new ServiceDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _serviceRepo.Verify(r =>
            r.Update(
                It.IsAny<Service>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenServiceExists_ReturnsSuccessAndDeletesFromRepository()
    {
        // Arrange
        var service = new Service { Id = 1, Name = "Haircut" };

        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(service);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeTrue();

        _serviceRepo.Verify(r =>
            r.Delete(
                It.Is<Service>(s => s.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenServiceNotFound_ReturnsSuccessWithNullDataAndNeverCallsDelete()
    {
        // Arrange
        _serviceRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);

        // Act
        var result = await _sut.Delete(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _serviceRepo.Verify(r =>
            r.Delete(
                It.IsAny<Service>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Service, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}