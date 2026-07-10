using BarberShop.API.Controllers;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BarberShop.Tests.Controllers;

public class UsersControllerTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUsersService> _usersService;
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        _usersService = new Mock<IUsersService>();
        _sut = new UsersController(_usersService.Object);
    }

    // Builds a controller HttpContext carrying the given JWT-derived claims.
    // JwtBearer maps "sub" to ClaimTypes.NameIdentifier by default, so tests
    // exercise the mapped variant (and one test covers the raw "sub" fallback).
    private void SetUserClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
    }

    private static UserResponseDTO MakeDto(int id = 6) => new()
    {
        Id = id,
        Email = "emily.johnson@example.com",
        UserRole = UserRoles.Client,
        CustomerId = 1,
        WorkerId = null,
        IsActive = true,
    };

    // =========================
    // GET /users/me
    // =========================

    [Fact]
    public async Task Me_WithNameIdentifierClaim_ReturnsOwnUser()
    {
        // Arrange
        SetUserClaims(new Claim(ClaimTypes.NameIdentifier, "6"));
        _usersService.Setup(s => s.GetByIdAsync(6)).ReturnsAsync(MakeDto(6));

        // Act
        var result = await _sut.Me();

        // Assert — any authenticated role can resolve its own profile.
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<UserResponseDTO>().Subject;
        dto.Id.Should().Be(6);
        dto.CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task Me_WithRawSubClaim_ReturnsOwnUser()
    {
        // Arrange — token pipeline configured without inbound claim mapping.
        SetUserClaims(new Claim("sub", "6"));
        _usersService.Setup(s => s.GetByIdAsync(6)).ReturnsAsync(MakeDto(6));

        // Act
        var result = await _sut.Me();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Me_WhenUserNoLongerExists_ReturnsNotFound()
    {
        // Arrange — account deleted after the token was issued.
        SetUserClaims(new Claim(ClaimTypes.NameIdentifier, "42"));
        _usersService.Setup(s => s.GetByIdAsync(42)).ReturnsAsync((UserResponseDTO?)null);

        // Act
        var result = await _sut.Me();

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Me_WithoutSubjectClaim_ReturnsUnauthorized()
    {
        // Arrange — authenticated principal but no usable subject claim.
        SetUserClaims(new Claim(ClaimTypes.Email, "x@x.com"));

        // Act
        var result = await _sut.Me();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _usersService.Verify(s => s.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }
}
