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

public class AppointmentsControllerTests
{
    private readonly Mock<IAppointmentsService> _service;
    private readonly Mock<IAppointmentAccessService> _access;
    private readonly AppointmentsController _sut;

    public AppointmentsControllerTests()
    {
        _service = new Mock<IAppointmentsService>();
        _access = new Mock<IAppointmentAccessService>();
        _sut = new AppointmentsController(_service.Object, _access.Object);
    }

    private void SetCaller(int userId, string role)
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

    // =========================
    // GET BY CUSTOMER
    // =========================

    [Fact]
    public async Task GetByCustomer_WhenNotOwner_ReturnsForbid()
    {
        SetCaller(10, "Client");
        _access.Setup(a => a.CanViewCustomerAsync(10, false, 2)).ReturnsAsync(false);

        var result = await _sut.GetByCustomer(2);

        result.Should().BeOfType<ForbidResult>();
        _service.Verify(s => s.GetByCustomer(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetByCustomer_WhenOwner_ReturnsData()
    {
        SetCaller(10, "Client");
        _access.Setup(a => a.CanViewCustomerAsync(10, false, 1)).ReturnsAsync(true);
        _service.Setup(s => s.GetByCustomer(1)).ReturnsAsync([]);

        var result = await _sut.GetByCustomer(1);

        result.Should().BeOfType<OkObjectResult>();
    }

    // =========================
    // GET BY WORKER
    // =========================

    [Fact]
    public async Task GetByWorker_WhenNotOwner_ReturnsForbid()
    {
        SetCaller(20, "User");
        _access.Setup(a => a.CanViewWorkerAsync(20, false, 6)).ReturnsAsync(false);

        var result = await _sut.GetByWorker(6);

        result.Should().BeOfType<ForbidResult>();
        _service.Verify(s => s.GetByWorker(It.IsAny<int>()), Times.Never);
    }

    // =========================
    // CANCEL
    // =========================

    [Fact]
    public async Task Cancel_WhenNotOwner_ReturnsForbidAndDoesNotCancel()
    {
        SetCaller(10, "Client");
        var dto = new CancelAppointmentsDTO { IdList = [50] };
        _access.Setup(a => a.CanMutateAsync(10, false, dto.IdList)).ReturnsAsync(false);

        var result = await _sut.Cancel(dto);

        result.Should().BeOfType<ForbidResult>();
        _service.Verify(s => s.CancelAppointments(It.IsAny<List<int>>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_WhenOwner_CancelsAppointments()
    {
        SetCaller(10, "Client");
        var dto = new CancelAppointmentsDTO { IdList = [50] };
        _access.Setup(a => a.CanMutateAsync(10, false, dto.IdList)).ReturnsAsync(true);
        _service.Setup(s => s.CancelAppointments(dto.IdList))
            .ReturnsAsync(Result<List<AppointmentResponseDTO>>.Ok([]));

        var result = await _sut.Cancel(dto);

        result.Should().BeOfType<OkObjectResult>();
        _service.Verify(s => s.CancelAppointments(dto.IdList), Times.Once);
    }

    [Fact]
    public async Task Cancel_WhenAdmin_BypassesOwnershipButStillChecksAccess()
    {
        // Admins are allowed by the access service returning true.
        SetCaller(1, "Admin");
        var dto = new CancelAppointmentsDTO { IdList = [50, 51] };
        _access.Setup(a => a.CanMutateAsync(1, true, dto.IdList)).ReturnsAsync(true);
        _service.Setup(s => s.CancelAppointments(dto.IdList))
            .ReturnsAsync(Result<List<AppointmentResponseDTO>>.Ok([]));

        var result = await _sut.Cancel(dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cancel_WithEmptyList_ReturnsBadRequest()
    {
        SetCaller(10, "Client");

        var result = await _sut.Cancel(new CancelAppointmentsDTO { IdList = [] });

        result.Should().BeOfType<BadRequestObjectResult>();
        _access.Verify(a => a.CanMutateAsync(
            It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IEnumerable<int>>()), Times.Never);
    }

    // =========================
    // CREATE RECURRING
    // =========================

    [Fact]
    public async Task CreateRecurring_WhenServiceSucceeds_ReturnsOk()
    {
        var dto = new RecurringAppointmentRequestDTO
        {
            WorkerId = 1, CustomerId = 1, ServiceId = 1,
            ScheduledFor = DateTime.Parse("2026-07-10T14:00:00"), RepeatWeeks = 3,
        };
        var resultDto = new RecurringAppointmentResultDTO { RecurrenceId = Guid.NewGuid() };
        _service.Setup(s => s.CreateRecurring(dto))
            .ReturnsAsync(Result<RecurringAppointmentResultDTO>.Ok(resultDto));

        var result = await _sut.CreateRecurring(dto);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(resultDto);
    }

    [Fact]
    public async Task CreateRecurring_WhenServiceFails_ReturnsBadRequest()
    {
        var dto = new RecurringAppointmentRequestDTO { RepeatWeeks = 20 };
        _service.Setup(s => s.CreateRecurring(dto))
            .ReturnsAsync(Result<RecurringAppointmentResultDTO>.Fail("RepeatWeeks must be between 1 and 12"));

        var result = await _sut.CreateRecurring(dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
