using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class CustomersServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<ICustomerRepository> _customerRepo;
    private readonly Mock<IRedisService> _redis;
    private readonly Mock<INotificationPublisher> _notifications;
    private readonly IMapper _mapper;
    private readonly CustomersService _sut;

    public CustomersServiceTests()
    {
        _customerRepo = new Mock<ICustomerRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Customers).Returns(_customerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _redis = new Mock<IRedisService>();
        _redis.Setup(r => r.GetAsync<List<CustomerDTO>>(It.IsAny<string>()))
            .ReturnsAsync((List<CustomerDTO>?)null);
        _redis.Setup(r => r.GetAsync<CustomerDTO>(It.IsAny<string>()))
            .ReturnsAsync((CustomerDTO?)null);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<List<CustomerDTO>>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<CustomerDTO>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.InvalidateByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _notifications = new Mock<INotificationPublisher>();
        _notifications.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MappingProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new CustomersService(_uow.Object, _mapper, _redis.Object, _notifications.Object, NullLogger<CustomersService>.Instance);
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

    [Fact]
    public async Task Delete_WhenCustomerHasLinkedRecords_ReturnsFriendlyErrorInsteadOfThrowing()
    {
        // Regression test: deleting a customer with existing appointments/reviews
        // used to bubble up an unhandled DbUpdateException (SQL error 547) as a
        // raw 500 — CustomersService.Delete must catch it and fail gracefully.
        var customer = MakeCustomer(1);
        _customerRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(customer);
        _uow.Setup(u => u.SaveAsync()).ThrowsAsync(new InvalidOperationException("FK violation"));

        var result = await _sut.Delete(1);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("existing appointments");
    }
}