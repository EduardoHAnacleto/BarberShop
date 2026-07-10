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

public class UsersServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IRedisService> _redis;
    private readonly Mock<INotificationPublisher> _notifications;
    private readonly Mock<ISecurityStampService> _stamps;
    private readonly IMapper _mapper;
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _redis = new Mock<IRedisService>();
        _redis.Setup(r => r.GetAsync<List<UserResponseDTO>>(It.IsAny<string>()))
            .ReturnsAsync((List<UserResponseDTO>?)null);
        _redis.Setup(r => r.GetAsync<UserResponseDTO>(It.IsAny<string>()))
            .ReturnsAsync((UserResponseDTO?)null);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<List<UserResponseDTO>>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<UserResponseDTO>(), It.IsAny<TimeSpan?>()))
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

        _stamps = new Mock<ISecurityStampService>();
        _stamps.Setup(s => s.InvalidateCacheAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _sut = new UsersService(_uow.Object, _mapper, _redis.Object, _notifications.Object, NullLogger<UsersService>.Instance, _stamps.Object);
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
    public async Task Update_WhenEmailChanges_RotatesSecurityStamp()
    {
        // Arrange
        var existing = MakeUser(1, "old@barbershop.com");
        var originalStamp = existing.SecurityStamp;

        var dto = MakeUserRequestDTO("new@barbershop.com");
        dto.PasswordHash = ""; // keep current password

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, dto);

        // Assert — email change must revoke previously issued tokens.
        existing.SecurityStamp.Should().NotBe(originalStamp);
        _stamps.Verify(s => s.InvalidateCacheAsync(1), Times.Once);
    }

    [Fact]
    public async Task Update_WhenPasswordProvided_RotatesSecurityStampAndRehashes()
    {
        // Arrange
        var existing = MakeUser(1);
        var originalStamp = existing.SecurityStamp;
        var originalHash = existing.PasswordHash;

        var dto = MakeUserRequestDTO(existing.Email);
        dto.PasswordHash = "NewPassword@123";

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, dto);

        // Assert
        existing.SecurityStamp.Should().NotBe(originalStamp);
        existing.PasswordHash.Should().NotBe(originalHash);
        BCrypt.Net.BCrypt.Verify("NewPassword@123", existing.PasswordHash).Should().BeTrue();
        _stamps.Verify(s => s.InvalidateCacheAsync(1), Times.Once);
    }

    [Fact]
    public async Task Update_WhenCredentialsUnchanged_KeepsStampAndPassword()
    {
        // Arrange — same email, empty password means "keep current password".
        var existing = MakeUser(1);
        var originalStamp = existing.SecurityStamp;
        var originalHash = existing.PasswordHash;

        var dto = MakeUserRequestDTO(existing.Email);
        dto.PasswordHash = "";
        dto.IsActive = false; // a non-credential field being edited

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(existing);

        // Act
        await _sut.Update(1, dto);

        // Assert — sessions survive edits that do not touch credentials, and
        // the stored hash must not be replaced by a hash of the empty string.
        existing.SecurityStamp.Should().Be(originalStamp);
        existing.PasswordHash.Should().Be(originalHash);
        existing.IsActive.Should().BeFalse();
        _stamps.Verify(s => s.InvalidateCacheAsync(It.IsAny<int>()), Times.Never);
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