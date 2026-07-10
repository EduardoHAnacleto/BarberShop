using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Moq;

namespace BarberShop.Tests.Services;

public class AppointmentAccessServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly AppointmentAccessService _sut;

    public AppointmentAccessServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _appointmentRepo = new Mock<IAppointmentRepository>();

        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);

        _sut = new AppointmentAccessService(_uow.Object);
    }

    // User 10 is the client linked to customer 1; user 20 is the worker
    // linked to worker 5.
    private void SetupClient(int userId = 10, int customerId = 1) =>
        _userRepo
            .Setup(r => r.GetByIdAsync(
                userId,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(new User { Id = userId, CustomerId = customerId, Email = "c@b.com" });

    private void SetupWorker(int userId = 20, int workerId = 5) =>
        _userRepo
            .Setup(r => r.GetByIdAsync(
                userId,
                It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()))
            .ReturnsAsync(new User { Id = userId, WorkerId = workerId, Email = "w@b.com" });

    private void SetupAppointment(int id, int customerId, int workerId) =>
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(new Appointment { Id = id, CustomerId = customerId, WorkerId = workerId });

    // =========================
    // ADMIN — ALWAYS ALLOWED
    // =========================

    [Fact]
    public async Task Admin_CanViewAnyCustomer()
    {
        (await _sut.CanViewCustomerAsync(999, isAdmin: true, customerId: 1))
            .Should().BeTrue();
        // Admin path must not even need a DB lookup.
        _userRepo.Verify(r => r.GetByIdAsync(
            It.IsAny<int>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<User, object>>[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Admin_CanViewAnyWorker()
        => (await _sut.CanViewWorkerAsync(999, isAdmin: true, workerId: 5))
            .Should().BeTrue();

    [Fact]
    public async Task Admin_CanMutateAnyAppointment()
        => (await _sut.CanMutateAsync(999, isAdmin: true, new[] { 1, 2, 3 }))
            .Should().BeTrue();

    // =========================
    // CLIENT — OWN CUSTOMER ONLY
    // =========================

    [Fact]
    public async Task Client_CanViewOwnCustomer()
    {
        SetupClient(10, customerId: 1);

        (await _sut.CanViewCustomerAsync(10, isAdmin: false, customerId: 1))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Client_CannotViewAnotherCustomer()
    {
        SetupClient(10, customerId: 1);

        (await _sut.CanViewCustomerAsync(10, isAdmin: false, customerId: 2))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Client_CannotViewWorkerAppointments()
    {
        SetupClient(10, customerId: 1);

        // A client has no linked workerId, so worker views are denied.
        (await _sut.CanViewWorkerAsync(10, isAdmin: false, workerId: 5))
            .Should().BeFalse();
    }

    // =========================
    // WORKER — OWN WORKER ONLY
    // =========================

    [Fact]
    public async Task Worker_CanViewOwnWorker()
    {
        SetupWorker(20, workerId: 5);

        (await _sut.CanViewWorkerAsync(20, isAdmin: false, workerId: 5))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Worker_CannotViewAnotherWorker()
    {
        SetupWorker(20, workerId: 5);

        (await _sut.CanViewWorkerAsync(20, isAdmin: false, workerId: 6))
            .Should().BeFalse();
    }

    // =========================
    // MUTATION — OWNERSHIP
    // =========================

    [Fact]
    public async Task Client_CanMutateOwnAppointment()
    {
        SetupClient(10, customerId: 1);
        SetupAppointment(50, customerId: 1, workerId: 5);

        (await _sut.CanMutateAsync(10, isAdmin: false, new[] { 50 }))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Client_CannotMutateAnotherCustomersAppointment()
    {
        SetupClient(10, customerId: 1);
        SetupAppointment(50, customerId: 2, workerId: 5);

        (await _sut.CanMutateAsync(10, isAdmin: false, new[] { 50 }))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Worker_CanMutateAppointmentAssignedToThem()
    {
        SetupWorker(20, workerId: 5);
        SetupAppointment(50, customerId: 1, workerId: 5);

        (await _sut.CanMutateAsync(20, isAdmin: false, new[] { 50 }))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Mutation_DeniedWhenAnyAppointmentInBatchIsNotOwned()
    {
        SetupClient(10, customerId: 1);
        SetupAppointment(50, customerId: 1, workerId: 5); // owned
        SetupAppointment(51, customerId: 2, workerId: 5); // not owned

        (await _sut.CanMutateAsync(10, isAdmin: false, new[] { 50, 51 }))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Mutation_DeniedWhenAppointmentMissing()
    {
        SetupClient(10, customerId: 1);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(
                99,
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        (await _sut.CanMutateAsync(10, isAdmin: false, new[] { 99 }))
            .Should().BeFalse();
    }
}
