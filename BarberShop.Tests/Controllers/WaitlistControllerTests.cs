using BarberShop.API.Controllers;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BarberShop.Tests.Controllers;

public class WaitlistControllerTests
{
    private readonly Mock<IWaitlistService> _waitlist;
    private readonly Mock<IUsersService> _users;
    private readonly WaitlistController _sut;

    public WaitlistControllerTests()
    {
        _waitlist = new Mock<IWaitlistService>();
        _users = new Mock<IUsersService>();
        _sut = new WaitlistController(_waitlist.Object, _users.Object);
    }

    private void SetCaller(int userId, string role = "Client")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "TestAuth");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    [Fact]
    public async Task Join_UsesJwtResolvedCustomerId()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6)).ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _waitlist.Setup(w => w.Join(1, It.IsAny<WaitlistRequestDTO>()))
            .ReturnsAsync(Result<WaitlistResponseDTO>.Ok(new WaitlistResponseDTO { Id = 1, CustomerId = 1 }));

        var result = await _sut.Join(new WaitlistRequestDTO { WorkerId = 1, ServiceId = 1, PreferredDate = DateTime.Today });

        result.Should().BeOfType<CreatedAtActionResult>();
        _waitlist.Verify(w => w.Join(1, It.IsAny<WaitlistRequestDTO>()), Times.Once);
    }

    [Fact]
    public async Task Join_WhenNoLinkedCustomer_ReturnsForbid()
    {
        SetCaller(9);
        _users.Setup(u => u.GetByIdAsync(9)).ReturnsAsync(new UserResponseDTO { Id = 9, CustomerId = null });

        var result = await _sut.Join(new WaitlistRequestDTO());

        result.Should().BeOfType<ForbidResult>();
        _waitlist.Verify(w => w.Join(It.IsAny<int>(), It.IsAny<WaitlistRequestDTO>()), Times.Never);
    }

    [Fact]
    public async Task GetMine_ResolvesOwnCustomerFromJwt()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6)).ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _waitlist.Setup(w => w.GetMineAsync(1)).ReturnsAsync([new WaitlistResponseDTO { Id = 1, CustomerId = 1 }]);

        var result = await _sut.GetMine();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<WaitlistResponseDTO>>().Which.Should().HaveCount(1);
    }

    [Fact]
    public async Task Remove_WhenAdmin_CallsDeleteRegardlessOfOwnership()
    {
        SetCaller(1, "Admin");
        _waitlist.Setup(w => w.Delete(5)).ReturnsAsync(Result<bool>.Ok(true));

        var result = await _sut.Remove(5);

        result.Should().BeOfType<NoContentResult>();
        _waitlist.Verify(w => w.Delete(5), Times.Once);
        _waitlist.Verify(w => w.Leave(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Remove_WhenClient_CallsLeaveWithResolvedCustomerId()
    {
        SetCaller(6, "Client");
        _users.Setup(u => u.GetByIdAsync(6)).ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _waitlist.Setup(w => w.Leave(1, 5)).ReturnsAsync(Result<bool>.Ok(true));

        var result = await _sut.Remove(5);

        result.Should().BeOfType<NoContentResult>();
        _waitlist.Verify(w => w.Leave(1, 5), Times.Once);
        _waitlist.Verify(w => w.Delete(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Remove_WhenClientLeaveFails_ReturnsBadRequest()
    {
        SetCaller(6, "Client");
        _users.Setup(u => u.GetByIdAsync(6)).ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _waitlist.Setup(w => w.Leave(1, 5)).ReturnsAsync(Result<bool>.Fail("You can only remove your own waitlist entry"));

        var result = await _sut.Remove(5);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
