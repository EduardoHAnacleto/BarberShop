using BarberShop.API.Controllers;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BarberShop.Tests.Controllers;

public class ReportsControllerTests
{
    [Fact]
    public async Task GetSummary_ReturnsServiceResult()
    {
        var reports = new Mock<IReportsService>();
        reports.Setup(r => r.GetSummaryAsync())
            .ReturnsAsync(new ReportsSummaryDTO { TotalRevenue = 100.00m, CompletedCount = 4 });

        var sut = new ReportsController(reports.Object);

        var result = await sut.GetSummary();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ReportsSummaryDTO>().Subject;
        dto.TotalRevenue.Should().Be(100.00m);
        dto.CompletedCount.Should().Be(4);
    }
}
