using BarberShop.API.Controllers;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BarberShop.Tests.Controllers;

public class WorkerScheduleControllerTests
{
    private readonly Mock<IWorkerScheduleService> _service;
    private readonly WorkerScheduleController _sut;

    public WorkerScheduleControllerTests()
    {
        _service = new Mock<IWorkerScheduleService>();
        _sut = new WorkerScheduleController(_service.Object);
    }

    [Fact]
    public async Task GetByWorker_ReturnsOkWithOverrides()
    {
        _service.Setup(s => s.GetByWorkerAsync(1))
            .ReturnsAsync([new WorkerScheduleDTO { WorkerId = 1, DayOfWeek = DayOfWeek.Monday }]);

        var result = await _sut.GetByWorker(1);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<WorkerScheduleDTO>>().Which.Should().HaveCount(1);
    }

    [Fact]
    public async Task Upsert_WhenServiceSucceeds_ReturnsOk()
    {
        var dto = new WorkerScheduleDTO { IsOpen = true };
        _service.Setup(s => s.UpsertAsync(1, DayOfWeek.Monday, dto))
            .ReturnsAsync(Result<WorkerScheduleDTO>.Ok(dto));

        var result = await _sut.Upsert(1, DayOfWeek.Monday, dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Upsert_WhenServiceFails_ReturnsBadRequest()
    {
        var dto = new WorkerScheduleDTO { IsOpen = true };
        _service.Setup(s => s.UpsertAsync(99, DayOfWeek.Monday, dto))
            .ReturnsAsync(Result<WorkerScheduleDTO>.Fail("Worker not found"));

        var result = await _sut.Upsert(99, DayOfWeek.Monday, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveOverride_WhenFound_ReturnsNoContent()
    {
        _service.Setup(s => s.RemoveOverrideAsync(1, DayOfWeek.Monday))
            .ReturnsAsync(Result<bool>.Ok(true));

        var result = await _sut.RemoveOverride(1, DayOfWeek.Monday);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveOverride_WhenNotFound_ReturnsNotFound()
    {
        _service.Setup(s => s.RemoveOverrideAsync(1, DayOfWeek.Monday))
            .ReturnsAsync(Result<bool>.Ok(false));

        var result = await _sut.RemoveOverride(1, DayOfWeek.Monday);

        result.Should().BeOfType<NotFoundResult>();
    }
}
