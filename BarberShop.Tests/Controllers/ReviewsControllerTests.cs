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

public class ReviewsControllerTests
{
    private readonly Mock<IReviewsService> _reviews;
    private readonly Mock<IUsersService> _users;
    private readonly ReviewsController _sut;

    public ReviewsControllerTests()
    {
        _reviews = new Mock<IReviewsService>();
        _users = new Mock<IUsersService>();
        _sut = new ReviewsController(_reviews.Object, _users.Object);
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
    // GET /api/reviews/worker/{id}
    // =========================

    [Fact]
    public async Task GetByWorker_ReturnsServiceResult()
    {
        _reviews.Setup(r => r.GetByWorkerAsync(3))
            .ReturnsAsync([new ReviewResponseDTO { Id = 1, WorkerId = 3 }]);

        var result = await _sut.GetByWorker(3);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<ReviewResponseDTO>>().Which.Should().HaveCount(1);
    }

    // =========================
    // GET /api/reviews/summary
    // =========================

    [Fact]
    public async Task GetSummary_ReturnsServiceResult()
    {
        _reviews.Setup(r => r.GetSummaryAsync())
            .ReturnsAsync([new WorkerRatingSummaryDTO { WorkerId = 1, AverageRating = 4.5, ReviewCount = 2 }]);

        var result = await _sut.GetSummary();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<WorkerRatingSummaryDTO>>().Which.Should().HaveCount(1);
    }

    // =========================
    // GET /api/reviews/mine
    // =========================

    [Fact]
    public async Task GetMine_ResolvesOwnCustomerFromJwt()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _reviews.Setup(r => r.GetMineAsync(1))
            .ReturnsAsync([new ReviewResponseDTO { Id = 1, CustomerId = 1 }]);

        var result = await _sut.GetMine();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<ReviewResponseDTO>>().Which.Should().HaveCount(1);
        _reviews.Verify(r => r.GetMineAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetMine_WhenNoLinkedCustomer_ReturnsEmptyArray()
    {
        SetCaller(9);
        _users.Setup(u => u.GetByIdAsync(9))
            .ReturnsAsync(new UserResponseDTO { Id = 9, CustomerId = null });

        var result = await _sut.GetMine();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ReviewResponseDTO[]>().Which.Should().BeEmpty();
        _reviews.Verify(r => r.GetMineAsync(It.IsAny<int>()), Times.Never);
    }

    // =========================
    // POST /api/reviews
    // =========================

    [Fact]
    public async Task Create_UsesJwtResolvedCustomerId()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _reviews.Setup(r => r.Create(1, It.IsAny<ReviewRequestDTO>()))
            .ReturnsAsync(Result<ReviewResponseDTO>.Ok(new ReviewResponseDTO { Id = 1, CustomerId = 1, WorkerId = 4 }));

        var result = await _sut.Create(new ReviewRequestDTO { AppointmentId = 10, Rating = 5 });

        result.Should().BeOfType<CreatedAtActionResult>();
        _reviews.Verify(r => r.Create(1, It.IsAny<ReviewRequestDTO>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenNoLinkedCustomer_ReturnsForbid()
    {
        SetCaller(9);
        _users.Setup(u => u.GetByIdAsync(9))
            .ReturnsAsync(new UserResponseDTO { Id = 9, CustomerId = null });

        var result = await _sut.Create(new ReviewRequestDTO { AppointmentId = 10, Rating = 5 });

        result.Should().BeOfType<ForbidResult>();
        _reviews.Verify(r => r.Create(It.IsAny<int>(), It.IsAny<ReviewRequestDTO>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenServiceFails_ReturnsBadRequest()
    {
        SetCaller(6);
        _users.Setup(u => u.GetByIdAsync(6))
            .ReturnsAsync(new UserResponseDTO { Id = 6, CustomerId = 1 });
        _reviews.Setup(r => r.Create(1, It.IsAny<ReviewRequestDTO>()))
            .ReturnsAsync(Result<ReviewResponseDTO>.Fail("Only completed appointments can be reviewed"));

        var result = await _sut.Create(new ReviewRequestDTO { AppointmentId = 10, Rating = 5 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // =========================
    // DELETE /api/reviews/{id}
    // =========================

    [Fact]
    public async Task Delete_WhenSuccessful_ReturnsNoContent()
    {
        _reviews.Setup(r => r.Delete(1)).ReturnsAsync(Result<ReviewResponseDTO>.Ok(null));

        var result = await _sut.Delete(1);

        result.Should().BeOfType<NoContentResult>();
    }
}
