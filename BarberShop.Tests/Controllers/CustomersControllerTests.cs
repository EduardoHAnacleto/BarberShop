using BarberShop.API.Controllers;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BarberShop.Tests.Controllers;

public class CustomersControllerTests
{
    private readonly Mock<ICustomersService> _customers;
    private readonly Mock<IUsersService> _users;
    private readonly Mock<ILoyaltyService> _loyalty;
    private readonly CustomersController _sut;

    public CustomersControllerTests()
    {
        _customers = new Mock<ICustomersService>();
        _users = new Mock<IUsersService>();
        _loyalty = new Mock<ILoyaltyService>();
        _sut = new CustomersController(_customers.Object, _users.Object, _loyalty.Object);
    }

    private void SetCaller(int userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    // =========================
    // GET /api/customers/me
    // =========================

    [Fact]
    public async Task GetMe_ResolvesOwnCustomerFromJwt()
    {
        // Arrange — user 6 is linked to customer 1.
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1, UserRole = UserRoles.Client });
        _customers.Setup(c => c.GetByIdAsync(1))
            .ReturnsAsync(new CustomerDTO { Id = 1, Name = "Emily" });

        // Act
        var result = await _sut.GetMe();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<CustomerDTO>().Which.Id.Should().Be(1);
        // Must never read a customer id supplied by the caller — only their own.
        _customers.Verify(c => c.GetByIdAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetMe_WhenNoLinkedCustomer_ReturnsNotFound()
    {
        SetCaller(9);
        _users.Setup(u => u.GetByIdAsync(9))
            .ReturnsAsync(new UserResponseDTO { Id = 9, CustomerId = null, UserRole = UserRoles.User });

        var result = await _sut.GetMe();

        result.Should().BeOfType<NotFoundResult>();
        _customers.Verify(c => c.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    // =========================
    // GET /api/customers/me/loyalty
    // =========================

    [Fact]
    public async Task GetMyLoyalty_ResolvesOwnCustomerFromJwt()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _loyalty.Setup(l => l.GetStatusAsync(1))
            .ReturnsAsync(new LoyaltyStatusDTO { CompletedVisits = 3, VisitsForReward = 10, VisitsUntilReward = 7 });

        var result = await _sut.GetMyLoyalty();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<LoyaltyStatusDTO>().Which.CompletedVisits.Should().Be(3);
        _loyalty.Verify(l => l.GetStatusAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetMyLoyalty_WhenNoLinkedCustomer_ReturnsNotFound()
    {
        SetCaller(9);
        _users.Setup(u => u.GetByIdAsync(9))
            .ReturnsAsync(new UserResponseDTO { Id = 9, CustomerId = null });

        var result = await _sut.GetMyLoyalty();

        result.Should().BeOfType<NotFoundResult>();
        _loyalty.Verify(l => l.GetStatusAsync(It.IsAny<int>()), Times.Never);
    }

    // =========================
    // PUT /api/customers/me
    // =========================

    [Fact]
    public async Task UpdateMe_UpdatesTheCallersOwnCustomerRecord()
    {
        // Arrange — caller 6 → customer 1; the route id cannot be spoofed.
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _customers.Setup(c => c.Update(1, It.IsAny<CustomerDTO>()))
            .ReturnsAsync(Result<CustomerDTO>.Ok(new CustomerDTO { Id = 1, Name = "Emily R." }));

        // Act
        var result = await _sut.UpdateMe(new CustomerDTO { Id = 999, Name = "Emily R." });

        // Assert — the caller's own id (1) is used, not the DTO's spoofed 999.
        result.Should().BeOfType<OkObjectResult>();
        _customers.Verify(c => c.Update(1, It.IsAny<CustomerDTO>()), Times.Once);
        _customers.Verify(c => c.Update(999, It.IsAny<CustomerDTO>()), Times.Never);
    }
}
