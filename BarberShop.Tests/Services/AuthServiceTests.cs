using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Enums;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BarberShop.Tests.Services;

public class AuthServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _config = BuildConfig();
        _tokenService = new TokenService(_config);

        _sut = new AuthService(
            _uow.Object,
            _tokenService,
            NullLogger<AuthService>.Instance,
            _config);
    }

    private static IConfiguration BuildConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]              = "BarberShopSuperSecretKeyForTests32Chars!",
                ["Jwt:Issuer"]           = "BarberShop.API",
                ["Jwt:Audience"]         = "BarberShop.Client",
                ["Jwt:ExpiresInMinutes"] = "60"
            })
            .Build();
    }

    // =========================
    // HELPERS
    // =========================
    private static User MakeUser(
        int id = 1,
        string email = "john@barbershop.com",
        string password = "password123",
        bool isActive = true,
        int failedAttempts = 0,
        DateTime? lockoutEnd = null) => new()
        {
            Id = id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            UserRole = UserRoles.Client,
            IsActive = isActive,
            FailedLoginAttempts = failedAttempts,
            LockoutEnd = lockoutEnd,
            CreatedAt = DateTime.UtcNow
        };

    private static LoginDTO MakeLoginDTO(
        string email = "john@barbershop.com",
        string password = "password123") => new()
        {
            Email = email,
            Password = password
        };

    // =========================
    // LOGIN — SUCCESS
    // =========================

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccessWithToken()
    {
        // Arrange
        var user = MakeUser();

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().NotBeNullOrEmpty();
        result.Data.Email.Should().Be("john@barbershop.com");
        result.Data.UserRole.Should().Be(UserRoles.Client.ToString());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ResetsFailedAttemptsAndLockout()
    {
        // Arrange
        var user = MakeUser(failedAttempts: 3);

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
        user.LockoutEnd.Should().BeNull();

        _userRepo.Verify(r =>
            r.Update(It.Is<User>(u => u.FailedLoginAttempts == 0),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // LOGIN — USER NOT FOUND / INACTIVE
    // =========================

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsFail()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid credentials");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsInactive_ReturnsFail()
    {
        // Arrange
        var user = MakeUser(isActive: false);

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid credentials");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // LOGIN — LOCKOUT
    // =========================

    [Fact]
    public async Task LoginAsync_WhenAccountIsLocked_ReturnsFail()
    {
        // Arrange
        var user = MakeUser(lockoutEnd: DateTime.UtcNow.AddMinutes(10));

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Account is locked");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenLockoutExpired_AllowsLogin()
    {
        // Arrange — lockout no passado
        var user = MakeUser(lockoutEnd: DateTime.UtcNow.AddMinutes(-1));

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert
        result.Success.Should().BeTrue();
    }

    // =========================
    // LOGIN — WRONG PASSWORD
    // =========================

    [Fact]
    public async Task LoginAsync_WithWrongPassword_IncreasesFailedAttempts()
    {
        // Arrange
        var user = MakeUser(failedAttempts: 0);

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO(password: "wrongpassword"));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid credentials");
        user.FailedLoginAttempts.Should().Be(1);
        user.LockoutEnd.Should().BeNull();

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenFailedAttemptsReachFive_LocksAccount()
    {
        // Arrange — já está em 4 tentativas, a próxima trava
        var user = MakeUser(failedAttempts: 4);

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO(password: "wrongpassword"));

        // Assert
        result.Success.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(5);
        user.LockoutEnd.Should().NotBeNull();
        user.LockoutEnd.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    // =========================
    // UNLOCK USER
    // =========================

    [Fact]
    public async Task UnlockUserAsync_WhenUserExists_UnlocksAndReturnsTrue()
    {
        // Arrange
        var user = MakeUser(
            failedAttempts: 5,
            lockoutEnd: DateTime.UtcNow.AddMinutes(10));

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.UnlockUserAsync(1);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        user.LockoutEnd.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);

        _userRepo.Verify(r =>
            r.Update(
                It.Is<User>(u => u.LockoutEnd == null && u.FailedLoginAttempts == 0),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Once);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UnlockUserAsync_WhenUserNotFound_ReturnsFalse()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.UnlockUserAsync(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();

        _userRepo.Verify(r =>
            r.Update(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}
