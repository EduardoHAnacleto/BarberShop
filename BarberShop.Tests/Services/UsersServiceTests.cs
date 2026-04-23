using AutoMapper;
using BarberShop.API.Hubs;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using BarberShop.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace BarberShop.Tests.Services;

public class UsersServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IHubContext<UsersHub>> _hub;
    private readonly IMapper _mapper;
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        // Repositório e UoW
        _userRepo = new Mock<IUserRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        // Hub
        _hub = new Mock<IHubContext<UsersHub>>();
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

        _sut = new UsersService(_uow.Object, _mapper, BuildRedis(), _hub.Object);
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
    private static User MakeUser(int id = 1, string email = "john@barbershop.com") => new()
    {
        Id = id,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        UserRole = UserRoles.Client,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static UserRequestDTO MakeUserRequestDTO(string email = "john@barbershop.com") => new()
    {
        Email = email,
        PasswordHash = "password123",
        UserRole = UserRoles.Client,
        IsActive = true
    };

    // =========================
    // GET ALL
    // =========================

    [Fact]
    public async Task GetAllAsync_WhenUsersExist_ReturnsMappedDtoList()
    {
        // Arrange
        var users = new List<User>
        {
            MakeUser(1, "john@barbershop.com"),
            MakeUser(2, "jane@barbershop.com")
        };

        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(users);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Email.Should().Be("john@barbershop.com");
        result[1].Email.Should().Be("jane@barbershop.com");
    }

    [Fact]
    public async Task GetAllAsync_WhenNoUsersExist_ReturnsEmptyList()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
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
    public async Task GetByIdAsync_WhenUserExists_ReturnsMappedDto()
    {
        // Arrange
        var user = MakeUser(1);

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Email.Should().Be("john@barbershop.com");
        result.UserRole.Should().Be(UserRoles.Client);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

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
        var dto = MakeUserRequestDTO();

        // Simula que não existe usuário com esse email
        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync([]);

        _userRepo
            .Setup(r => r.AddAsync(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User u,
                System.Linq.Expressions.Expression<Func<User, object>>[] _) => u);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be("john@barbershop.com");
        result.Data.UserRole.Should().Be(UserRoles.Client);

        _userRepo.Verify(r =>
            r.AddAsync(
                It.Is<User>(u => u.Email == "john@barbershop.com"),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsFail()
    {
        // Arrange
        var dto = MakeUserRequestDTO();
        var existing = new List<User> { MakeUser(1) };

        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Email already exists");
        result.Data.Should().BeNull();

        _userRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Theory]
    [InlineData("", "Invalid Email")]
    [InlineData("  ", "Invalid Email")]
    [InlineData(null, "Invalid Email")]
    public async Task Create_WithInvalidEmail_ReturnsFail(string? email, string expectedError)
    {
        // Arrange
        var dto = MakeUserRequestDTO(email!);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);

        _userRepo.Verify(r =>
            r.AddAsync(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // UPDATE
    // =========================

    [Fact]
    public async Task Update_WhenUserExists_ReturnsSuccessWithUpdatedData()
    {
        // Arrange
        var existing = MakeUser(1);

        var dto = MakeUserRequestDTO("updated@barbershop.com");
        dto.UserRole = UserRoles.Admin;

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.Update(1, dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Email.Should().Be("updated@barbershop.com");
        result.Data.UserRole.Should().Be(UserRoles.Admin);

        _userRepo.Verify(r =>
            r.Update(
                It.Is<User>(u => u.Email == "updated@barbershop.com"),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WhenUserNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.Update(99, MakeUserRequestDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _userRepo.Verify(r =>
            r.Update(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenUserExists_ReturnsSuccessAndCallsRepository()
    {
        // Arrange
        var user = MakeUser(1);

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Success.Should().BeTrue();

        _userRepo.Verify(r =>
            r.Delete(
                It.Is<User>(u => u.Id == 1),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenUserNotFound_ReturnsSuccessWithNullData()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.Delete(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();

        _userRepo.Verify(r =>
            r.Delete(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}