using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace BarberShop.Tests.Services;

public class WaitlistServiceTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWaitlistRepository> _waitlistRepo;
    private readonly Mock<IWorkerRepository> _workerRepo;
    private readonly Mock<IServiceRepository> _serviceRepo;
    private readonly Mock<ICustomerRepository> _customerRepo;
    private readonly Mock<IEmailService> _email;
    private readonly WaitlistService _sut;

    private static readonly DateTime PreferredDate = new(2026, 8, 1);

    public WaitlistServiceTests()
    {
        _waitlistRepo = new Mock<IWaitlistRepository>();
        _workerRepo = new Mock<IWorkerRepository>();
        _serviceRepo = new Mock<IServiceRepository>();
        _customerRepo = new Mock<ICustomerRepository>();

        _workerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync(new Worker { Id = 1, Name = "James Carter" });
        _serviceRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Service, object>>[]>()))
            .ReturnsAsync(new Service { Id = 1, Name = "Haircut", Duration = 30 });
        _customerRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Customer, object>>[]>()))
            .ReturnsAsync(new Customer { Id = 1, Name = "Emily Johnson", Email = "emily@example.com" });
        _waitlistRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Waitlist, bool>>>(),
                It.IsAny<Func<IQueryable<Waitlist>, IOrderedQueryable<Waitlist>>>(),
                It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync([]);

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Waitlist).Returns(_waitlistRepo.Object);
        _uow.Setup(u => u.Workers).Returns(_workerRepo.Object);
        _uow.Setup(u => u.Services).Returns(_serviceRepo.Object);
        _uow.Setup(u => u.Customers).Returns(_customerRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _email = new Mock<IEmailService>();
        _email
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sut = new WaitlistService(_uow.Object, _email.Object, NullLogger<WaitlistService>.Instance);
    }

    private static Waitlist MakeEntry(int id, int customerId = 1, DateTime? notified = null) => new()
    {
        Id = id,
        CustomerId = customerId,
        Customer = new Customer { Id = customerId, Name = "Emily Johnson", Email = "emily@example.com" },
        WorkerId = 1,
        Worker = new Worker { Id = 1, Name = "James Carter" },
        ServiceId = 1,
        Service = new Service { Id = 1, Name = "Haircut" },
        PreferredDate = PreferredDate,
        NotifiedAt = notified,
    };

    // =========================
    // JOIN
    // =========================

    [Fact]
    public async Task Join_WithValidData_CreatesEntry()
    {
        _waitlistRepo
            .Setup(r => r.AddAsync(It.IsAny<Waitlist>(), It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync((Waitlist w, Expression<Func<Waitlist, object>>[] _) => w);

        var result = await _sut.Join(1, new WaitlistRequestDTO { WorkerId = 1, ServiceId = 1, PreferredDate = PreferredDate });

        result.Success.Should().BeTrue();
        result.Data!.WorkerName.Should().Be("James Carter");
        result.Data.ServiceName.Should().Be("Haircut");
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Join_WhenWorkerNotFound_Fails()
    {
        _workerRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<Expression<Func<Worker, object>>[]>()))
            .ReturnsAsync((Worker?)null);

        var result = await _sut.Join(1, new WaitlistRequestDTO { WorkerId = 99, ServiceId = 1, PreferredDate = PreferredDate });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Worker not found");
    }

    [Fact]
    public async Task Join_WhenServiceNotFound_Fails()
    {
        _serviceRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<Expression<Func<Service, object>>[]>()))
            .ReturnsAsync((Service?)null);

        var result = await _sut.Join(1, new WaitlistRequestDTO { WorkerId = 1, ServiceId = 99, PreferredDate = PreferredDate });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Service not found");
    }

    [Fact]
    public async Task Join_WhenAlreadyOnWaitlistForSameDay_Fails()
    {
        _waitlistRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Waitlist, bool>>>(),
                It.IsAny<Func<IQueryable<Waitlist>, IOrderedQueryable<Waitlist>>>(),
                It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync([MakeEntry(1)]);

        var result = await _sut.Join(1, new WaitlistRequestDTO { WorkerId = 1, ServiceId = 1, PreferredDate = PreferredDate });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("You're already on the waitlist for this day");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    // =========================
    // GET MINE
    // =========================

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallersEntries()
    {
        _waitlistRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Waitlist, bool>>>(),
                It.IsAny<Func<IQueryable<Waitlist>, IOrderedQueryable<Waitlist>>>(),
                It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync([MakeEntry(1)]);

        var result = await _sut.GetMineAsync(1);

        result.Should().HaveCount(1);
        result[0].CustomerName.Should().Be("Emily Johnson");
    }

    // =========================
    // LEAVE
    // =========================

    [Fact]
    public async Task Leave_WhenOwnedByCaller_RemovesEntry()
    {
        var entry = MakeEntry(1, customerId: 1);
        _waitlistRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync(entry);

        var result = await _sut.Leave(1, 1);

        result.Success.Should().BeTrue();
        _waitlistRepo.Verify(r => r.Delete(entry, It.IsAny<Expression<Func<Waitlist, object>>[]>()), Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Leave_WhenNotOwnedByCaller_Fails()
    {
        var entry = MakeEntry(1, customerId: 2);
        _waitlistRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync(entry);

        var result = await _sut.Leave(1, 1);

        result.Success.Should().BeFalse();
        _waitlistRepo.Verify(r => r.Delete(It.IsAny<Waitlist>(), It.IsAny<Expression<Func<Waitlist, object>>[]>()), Times.Never);
    }

    [Fact]
    public async Task Leave_WhenNotFound_Fails()
    {
        _waitlistRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync((Waitlist?)null);

        var result = await _sut.Leave(1, 99);

        result.Success.Should().BeFalse();
    }

    // =========================
    // NOTIFY WAITLIST FOR
    // =========================

    [Fact]
    public async Task NotifyWaitlistForAsync_EmailsUnnotifiedEntries_ForMatchingWorkerAndDate()
    {
        var entry = MakeEntry(1);
        _waitlistRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Waitlist, bool>>>(),
                It.IsAny<Func<IQueryable<Waitlist>, IOrderedQueryable<Waitlist>>>(),
                It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync([entry]);

        var count = await _sut.NotifyWaitlistForAsync(1, PreferredDate);

        count.Should().Be(1);
        entry.NotifiedAt.Should().NotBeNull();
        _email.Verify(e => e.SendAsync(
            "emily@example.com", "Emily Johnson", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task NotifyWaitlistForAsync_SkipsAlreadyNotifiedEntries()
    {
        var entry = MakeEntry(1, notified: DateTime.UtcNow.AddMinutes(-5));
        _waitlistRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Waitlist, bool>>>(),
                It.IsAny<Func<IQueryable<Waitlist>, IOrderedQueryable<Waitlist>>>(),
                It.IsAny<Expression<Func<Waitlist, object>>[]>()))
            .ReturnsAsync([entry]);

        var count = await _sut.NotifyWaitlistForAsync(1, PreferredDate);

        count.Should().Be(0);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyWaitlistForAsync_WhenNoOneWaiting_ReturnsZero()
    {
        var count = await _sut.NotifyWaitlistForAsync(1, PreferredDate);

        count.Should().Be(0);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}
