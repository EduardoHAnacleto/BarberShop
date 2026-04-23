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

public class CustomersServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<ICustomerRepository> _customerRepo;
    private readonly Mock<IHubContext<CustomersHub>> _hub;
    private readonly IMapper _mapper;
    private readonly CustomersService _sut;

    public CustomersServiceTests()
    {
        // Repositório e UoW
        _customerRepo = new Mock<ICustomerRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Customers).Returns(_customerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        // Hub
        _hub = new Mock<IHubContext<CustomersHub>>();
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

        _sut = new CustomersService(_uow.Object, _mapper, BuildRedis(), _hub.Object);
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
    private static Customer MakeCustomer(int id = 1, string name = "John Doe") => new()
    {
        Id = id,
        Name = name,
        Email = "john@email.com",
        PhoneNumber = "11999999999",
        DateOfBirth = new DateTime(1990, 1, 1)
    };

    private static CustomerDTO MakeCustomerDTO(string name = "John Doe") => new()
    {
        Name = name,
        Email = "john@email.com",
        PhoneNumber = "11999999999",
        DateOfBirth = new DateTime(1990, 1, 1)
    };

    // =========================
    // GET ALL
    // =========================

    [Fact]
    public async Task GetAllAsync_WhenCustomersExist_ReturnsMappedDtoList()
    {
        // Arrange
        var customers = new List<Customer>
        {
            MakeCustomer(1, "John Doe"),
            MakeCustomer(2, "Jane Doe")
        };

        _customerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, bool>>>(),
                It.IsAny<Func<IQueryable<Customer>, IOrderedQueryable<Customer>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(customers);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("John Doe");
        result[1].Name.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoCustomersExist_ReturnsEmptyList()
    {
        // Arrange
        _customerRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, bool>>>(),
                It.IsAny<Func<IQueryable<Customer>, IOrderedQueryable<Customer>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
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
    public async Task GetByIdAsync_WhenCustomerExists_ReturnsMappedDto()
    {
        // Arrange
        var customer = MakeCustomer(1);

        _customerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("John Doe");
        result.Email.Should().Be("john@email.com");
        result.PhoneNumber.Should().Be("11999999999");
    }

    [Fact]
    public async Task GetByIdAsync_WhenCustomerNotFound_ReturnsNull()
    {
        // Arrange
        _customerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer?)null);

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
        var dto = MakeCustomerDTO();

        _customerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer c,
                System.Linq.Expressions.Expression<Func<Customer, object>>[] _) => c);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("John Doe");
        result.Data.Email.Should().Be("john@email.com");

        _customerRepo.Verify(r =>
            r.AddAsync(
                It.Is<Customer>(c => c.Name == "John Doe"),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Theory]
    [InlineData("", "Invalid Name")]
    [InlineData("  ", "Invalid Name")]
    [InlineData(null, "Invalid Name")]
    public async Task Create_WithInvalidName_ReturnsFailWithExpectedError(
        string? name, string expectedError)
    {
        // Arrange
        var dto = MakeCustomerDTO(name!);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);
        result.Data.Should().BeNull();

        _customerRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // UPDATE
    // =========================

    [Fact]
    public async Task Update_WhenCustomerExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var existing = MakeCustomer(1);
        var dto = MakeCustomerDTO("Jane Updated");

        _customerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Jane Updated");

        _customerRepo.Verify(r =>
            r.Update(
                It.Is<Customer>(c => c.Name == "Jane Updated"),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenCustomerExists_SetsLastUpdatedAt()
    {
        // Arrange
        var existing = MakeCustomer(1);
        existing.LastUpdatedAt = null;

        _customerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, MakeCustomerDTO());

        // Assert
        existing.LastUpdatedAt.Should().NotBeNull();
        existing.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_WhenCustomerNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _customerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer?)null);

        // Act
        var result = await _sut.Update(99, MakeCustomerDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _customerRepo.Verify(r =>
            r.Update(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenCustomerExists_ReturnsSuccessAndCallsRepository()
    {
        // Arrange
        var customer = MakeCustomer(1);

        _customerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeTrue();

        _customerRepo.Verify(r =>
            r.Delete(
                It.Is<Customer>(c => c.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenCustomerNotFound_ReturnsSuccessWithNullDataAndNeverCallsDelete()
    {
        // Arrange
        _customerRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer?)null);

        // Act
        var result = await _sut.Delete(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _customerRepo.Verify(r =>
            r.Delete(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}