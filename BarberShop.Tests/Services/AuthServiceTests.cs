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
    private readonly Mock<ICustomerRepository> _customerRepo;
    private readonly Mock<ISecurityStampService> _stamps;
    private readonly Mock<IEmailService> _emailService;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _customerRepo = new Mock<ICustomerRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.Customers).Returns(_customerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);
        _uow.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);

        _stamps = new Mock<ISecurityStampService>();
        _stamps.Setup(s => s.InvalidateCacheAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _emailService = new Mock<IEmailService>();
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _config = BuildConfig();
        _tokenService = new TokenService(_config);

        _sut = new AuthService(
            _uow.Object,
            _tokenService,
            NullLogger<AuthService>.Instance,
            _config,
            _stamps.Object,
            _emailService.Object);
    }

    private static IConfiguration BuildConfig(string? googleClientId = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"]              = "BarberShopSuperSecretKeyForTests32Chars!",
            ["Jwt:Issuer"]           = "BarberShop.API",
            ["Jwt:Audience"]         = "BarberShop.Client",
            ["Jwt:ExpiresInMinutes"] = "60"
        };

        if (googleClientId != null)
            values["Google:ClientId"] = googleClientId;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
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
    // LOGOUT
    // =========================

    [Fact]
    public async Task LogoutAsync_WhenUserExists_RotatesStampAndInvalidatesCache()
    {
        // Arrange
        var user = MakeUser();
        var originalStamp = user.SecurityStamp;

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LogoutAsync(1);

        // Assert — a new stamp means every previously issued JWT stops validating.
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
        user.SecurityStamp.Should().NotBe(originalStamp);

        _uow.Verify(u => u.SaveAsync(), Times.Once);
        _stamps.Verify(s => s.InvalidateCacheAsync(1), Times.Once);
    }

    // =========================
    // CHANGE PASSWORD
    // =========================

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_UpdatesHashAndRotatesStamp()
    {
        // Arrange
        var user = MakeUser(password: "OldPass@123");
        var originalStamp = user.SecurityStamp;
        var originalHash = user.PasswordHash;

        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.ChangePasswordAsync(1, "OldPass@123", "NewPass@456");

        // Assert — new hash verifies, old sessions revoked.
        result.Success.Should().BeTrue();
        user.PasswordHash.Should().NotBe(originalHash);
        BCrypt.Net.BCrypt.Verify("NewPass@456", user.PasswordHash).Should().BeTrue();
        user.SecurityStamp.Should().NotBe(originalStamp);
        _stamps.Verify(s => s.InvalidateCacheAsync(1), Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Fails()
    {
        var user = MakeUser(password: "OldPass@123");
        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        var result = await _sut.ChangePasswordAsync(1, "WrongPass", "NewPass@456");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Current password is incorrect");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
        _stamps.Verify(s => s.InvalidateCacheAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WithTooShortNewPassword_Fails()
    {
        var user = MakeUser(password: "OldPass@123");
        _userRepo
            .Setup(r => r.GetByIdAsync(
                1,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(user);

        var result = await _sut.ChangePasswordAsync(1, "OldPass@123", "short");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("New password must be at least 8 characters");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenUserNotFound_Fails()
    {
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ChangePasswordAsync(99, "any", "NewPass@456");

        result.Success.Should().BeFalse();
    }

    // =========================
    // FORGOT / RESET PASSWORD
    // =========================

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailExists_IssuesTokenAndSendsEmail()
    {
        var user = MakeUser();
        _userRepo.Setup(r => r.GetByEmailAsync("john@barbershop.com")).ReturnsAsync(user);

        await _sut.ForgotPasswordAsync("john@barbershop.com");

        user.PasswordResetToken.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        user.PasswordResetTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(1));
        _uow.Verify(u => u.SaveAsync(), Times.Once);
        _emailService.Verify(e => e.SendAsync(
            "john@barbershop.com", "john@barbershop.com", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailDoesNotExist_DoesNotSendEmailOrThrow()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("nobody@barbershop.com")).ReturnsAsync((User?)null);

        // Must not reveal (via exception or behavior difference) that the email is unknown.
        await _sut.ForgotPasswordAsync("nobody@barbershop.com");

        _emailService.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenAccountInactive_DoesNotSendEmail()
    {
        var user = MakeUser(isActive: false);
        _userRepo.Setup(r => r.GetByEmailAsync("john@barbershop.com")).ReturnsAsync(user);

        await _sut.ForgotPasswordAsync("john@barbershop.com");

        _emailService.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_UpdatesPasswordAndRotatesStamp()
    {
        var user = MakeUser();
        var originalStamp = user.SecurityStamp;
        user.PasswordResetToken = "valid-token";
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);

        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync([user]);

        var result = await _sut.ResetPasswordAsync("valid-token", "BrandNewPass@1");

        result.Success.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("BrandNewPass@1", user.PasswordHash).Should().BeTrue();
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
        user.SecurityStamp.Should().NotBe(originalStamp);
        _stamps.Verify(s => s.InvalidateCacheAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_Fails()
    {
        var user = MakeUser();
        user.PasswordResetToken = "expired-token";
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5);

        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync([user]);

        var result = await _sut.ResetPasswordAsync("expired-token", "BrandNewPass@1");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid or expired reset link");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnknownToken_Fails()
    {
        _userRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<Func<IQueryable<User>, IOrderedQueryable<User>>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync([]);

        var result = await _sut.ResetPasswordAsync("no-such-token", "BrandNewPass@1");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid or expired reset link");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithTooShortPassword_Fails()
    {
        var result = await _sut.ResetPasswordAsync("some-token", "short");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("New password must be at least 8 characters");
    }

    [Fact]
    public async Task LogoutAsync_WhenUserNotFound_ReturnsFalse()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.LogoutAsync(99);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_IssuedToken_CarriesSecurityStampClaim()
    {
        // Arrange
        var user = MakeUser();

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.LoginAsync(MakeLoginDTO());

        // Assert — the stamp claim is what OnTokenValidated checks per request.
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(result.Data!.Token);

        jwt.Claims.Should().Contain(c =>
            c.Type == "stamp" && c.Value == user.SecurityStamp);
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

    // =========================
    // REGISTER
    // =========================

    private static RegisterDTO MakeRegisterDTO(
        string? name = "John Doe",
        string email = "john@barbershop.com",
        string password = "password123",
        string phone = "11999999999") => new()
        {
            Name = name,
            Email = email,
            Password = password,
            PhoneNumber = phone,
            DateOfBirth = new DateTime(1990, 1, 1)
        };

    [Fact]
    public async Task RegisterAsync_WithValidDto_ReturnsSuccessWithToken()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync((User?)null);

        _customerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer c, System.Linq.Expressions.Expression<Func<Customer, object>>[] _) => c);

        _userRepo
            .Setup(r => r.AddAsync(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User u, System.Linq.Expressions.Expression<Func<User, object>>[] _) => u);

        // Act
        var result = await _sut.RegisterAsync(MakeRegisterDTO());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Token.Should().NotBeNullOrEmpty();
        result.Data.Email.Should().Be("john@barbershop.com");
        result.Data.UserRole.Should().Be(UserRoles.Client.ToString());

        _uow.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _uow.Verify(u => u.CommitAsync(), Times.Once);
        _uow.Verify(u => u.RollbackAsync(), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFail()
    {
        // Arrange
        var existing = MakeUser(email: "john@barbershop.com");

        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync(existing);

        // Act
        var result = await _sut.RegisterAsync(MakeRegisterDTO());

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("This email is already registered.");

        _uow.Verify(u => u.BeginTransactionAsync(), Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Theory]
    [InlineData(null, "john@barbershop.com", "pass", "11999", "Name is required.")]
    [InlineData("", "john@barbershop.com", "pass", "11999", "Name is required.")]
    [InlineData("John", null, "pass", "11999", "Email is required.")]
    [InlineData("John", "", "pass", "11999", "Email is required.")]
    [InlineData("John", "john@x.com", null, "11999", "Password is required.")]
    [InlineData("John", "john@x.com", "", "11999", "Password is required.")]
    [InlineData("John", "john@x.com", "pass", null, "Phone number is required.")]
    [InlineData("John", "john@x.com", "pass", "", "Phone number is required.")]
    public async Task RegisterAsync_WithMissingRequiredField_ReturnsFail(
        string? name, string? email, string? password, string? phone, string expectedError)
    {
        // Act
        var result = await _sut.RegisterAsync(new RegisterDTO
        {
            Name = name!,
            Email = email ?? string.Empty,
            Password = password ?? string.Empty,
            PhoneNumber = phone ?? string.Empty
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(expectedError);

        _uow.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailToLowercase()
    {
        // Arrange
        _userRepo
            .Setup(r => r.GetByEmailAsync("john@barbershop.com"))
            .ReturnsAsync((User?)null);

        _customerRepo
            .Setup(r => r.AddAsync(
                It.IsAny<Customer>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync((Customer c, System.Linq.Expressions.Expression<Func<Customer, object>>[] _) => c);

        _userRepo
            .Setup(r => r.AddAsync(
                It.IsAny<User>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync((User u, System.Linq.Expressions.Expression<Func<User, object>>[] _) => u);

        // Act — email in mixed case
        var result = await _sut.RegisterAsync(MakeRegisterDTO(email: "JOHN@BARBERSHOP.COM"));

        // Assert — stored and returned as lowercase
        result.Success.Should().BeTrue();
        result.Data!.Email.Should().Be("john@barbershop.com");
    }

    // =========================
    // GOOGLE LOGIN
    // =========================

    [Fact]
    public async Task GoogleLoginAsync_WhenClientIdNotConfigured_ReturnsFail()
    {
        // Build a service with no Google:ClientId in config
        var configWithoutGoogle = BuildConfig(googleClientId: null);
        var sut = new AuthService(
            _uow.Object,
            new TokenService(configWithoutGoogle),
            NullLogger<AuthService>.Instance,
            configWithoutGoogle,
            _stamps.Object,
            _emailService.Object);

        // Act
        var result = await sut.GoogleLoginAsync(new GoogleLoginDTO { IdToken = "any-token" });

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Google login is not configured");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task GoogleLoginAsync_WithInvalidToken_ReturnsFail()
    {
        // Build a service with a Google:ClientId configured
        var configWithGoogle = BuildConfig(googleClientId: "test-client-id.apps.googleusercontent.com");
        var sut = new AuthService(
            _uow.Object,
            new TokenService(configWithGoogle),
            NullLogger<AuthService>.Instance,
            configWithGoogle,
            _stamps.Object,
            _emailService.Object);

        // Act — "bad-token" will cause GoogleJsonWebSignature.ValidateAsync to throw
        var result = await sut.GoogleLoginAsync(new GoogleLoginDTO { IdToken = "bad-token" });

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid Google token");

        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}
