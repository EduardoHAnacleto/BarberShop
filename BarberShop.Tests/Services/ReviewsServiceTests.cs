using AutoMapper;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace BarberShop.Tests.Services;

public class ReviewsServiceTests
{
    // =========================
    // SETUP
    // =========================
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IReviewRepository> _reviewRepo;
    private readonly Mock<IAppointmentRepository> _appointmentRepo;
    private readonly Mock<IRedisService> _redis;
    private readonly Mock<INotificationPublisher> _notifications;
    private readonly IMapper _mapper;
    private readonly ReviewsService _sut;

    public ReviewsServiceTests()
    {
        _reviewRepo = new Mock<IReviewRepository>();
        _appointmentRepo = new Mock<IAppointmentRepository>();
        _uow = new Mock<IUnitOfWork>();
        _uow.Setup(u => u.Reviews).Returns(_reviewRepo.Object);
        _uow.Setup(u => u.Appointments).Returns(_appointmentRepo.Object);
        _uow.Setup(u => u.SaveAsync()).ReturnsAsync(1);

        _redis = new Mock<IRedisService>();
        _redis.Setup(r => r.GetAsync<List<ReviewResponseDTO>>(It.IsAny<string>()))
            .ReturnsAsync((List<ReviewResponseDTO>?)null);
        _redis.Setup(r => r.GetAsync<List<WorkerRatingSummaryDTO>>(It.IsAny<string>()))
            .ReturnsAsync((List<WorkerRatingSummaryDTO>?)null);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<List<ReviewResponseDTO>>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<List<WorkerRatingSummaryDTO>>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.InvalidateByPrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _notifications = new Mock<INotificationPublisher>();
        _notifications.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(MappingProfile).Assembly);
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new ReviewsService(_uow.Object, _mapper, _redis.Object, _notifications.Object, NullLogger<ReviewsService>.Instance);
    }

    private static Appointment CompletedAppointment(int id = 1, int customerId = 1, int workerId = 1) => new()
    {
        Id = id,
        CustomerId = customerId,
        Customer = new Customer { Id = customerId, Name = "Emily Johnson" },
        WorkerId = workerId,
        Worker = new Worker { Id = workerId, Name = "James Carter" },
        ServiceId = 1,
        Service = new Service { Id = 1, Name = "Haircut", Duration = 30, Price = 25.00m },
        Status = Status.Completed,
        ScheduledFor = DateTime.UtcNow.AddDays(-1),
    };

    private void SetupNoExistingReview()
    {
        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync([]);
    }

    // =========================
    // CREATE
    // =========================

    [Fact]
    public async Task Create_WhenAppointmentNotFound_ReturnsFail()
    {
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync((Appointment?)null);

        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 99, Rating = 5 });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Appointment not found");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenAppointmentBelongsToDifferentCustomer_ReturnsFail()
    {
        var appointment = CompletedAppointment(customerId: 2);
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);

        // Caller is customer 1, appointment belongs to customer 2.
        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 1, Rating = 5 });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("You can only review your own appointments");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenAppointmentNotCompleted_ReturnsFail()
    {
        var appointment = CompletedAppointment();
        appointment.Status = Status.Scheduled;
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);

        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 1, Rating = 5 });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Only completed appointments can be reviewed");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Create_WhenRatingOutOfRange_ReturnsFail(int rating)
    {
        var appointment = CompletedAppointment();
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);

        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 1, Rating = rating });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Rating must be between 1 and 5");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WhenAlreadyReviewed_ReturnsFail()
    {
        var appointment = CompletedAppointment();
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);

        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync([new Review { Id = 5, AppointmentId = 1 }]);

        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 1, Rating = 4 });

        result.Success.Should().BeFalse();
        result.Error.Should().Be("This appointment has already been reviewed");
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsSuccessWithMappedDtoAndSaves()
    {
        var appointment = CompletedAppointment();
        _appointmentRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Appointment, object>>[]>()))
            .ReturnsAsync(appointment);
        SetupNoExistingReview();

        _reviewRepo
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync((Review r, Expression<Func<Review, object>>[] _) => r);

        var result = await _sut.Create(1, new ReviewRequestDTO { AppointmentId = 1, Rating = 5, Comment = "Great cut!" });

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Rating.Should().Be(5);
        result.Data.Comment.Should().Be("Great cut!");
        result.Data.CustomerName.Should().Be("Emily Johnson");
        result.Data.WorkerName.Should().Be("James Carter");
        result.Data.ServiceName.Should().Be("Haircut");

        _reviewRepo.Verify(r => r.AddAsync(
            It.Is<Review>(rv => rv.AppointmentId == 1 && rv.CustomerId == 1 && rv.WorkerId == 1 && rv.Rating == 5),
            It.IsAny<Expression<Func<Review, object>>[]>()), Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    // =========================
    // GET BY WORKER
    // =========================

    [Fact]
    public async Task GetByWorkerAsync_ReturnsOnlyThatWorkersReviews()
    {
        var appointment = CompletedAppointment();
        var review = new Review
        {
            Id = 1,
            AppointmentId = 1,
            Appointment = appointment,
            CustomerId = 1,
            Customer = appointment.Customer,
            WorkerId = 1,
            Worker = appointment.Worker,
            Rating = 5,
            Comment = "Excellent",
            CreatedAt = DateTime.UtcNow,
        };

        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync([review]);

        var result = await _sut.GetByWorkerAsync(1);

        result.Should().HaveCount(1);
        result[0].WorkerName.Should().Be("James Carter");
        result[0].Rating.Should().Be(5);
    }

    // =========================
    // GET SUMMARY
    // =========================

    [Fact]
    public async Task GetSummaryAsync_ComputesAverageAndCountPerWorker()
    {
        var appointment = CompletedAppointment();
        var reviews = new List<Review>
        {
            new() { Id = 1, WorkerId = 1, Worker = appointment.Worker, Rating = 5, Appointment = appointment, Customer = appointment.Customer },
            new() { Id = 2, WorkerId = 1, Worker = appointment.Worker, Rating = 3, Appointment = appointment, Customer = appointment.Customer },
            new() { Id = 3, WorkerId = 2, Worker = new Worker { Id = 2, Name = "Other" }, Rating = 4, Appointment = appointment, Customer = appointment.Customer },
        };

        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync(reviews);

        var result = await _sut.GetSummaryAsync();

        result.Should().HaveCount(2);
        var worker1 = result.Single(r => r.WorkerId == 1);
        worker1.ReviewCount.Should().Be(2);
        worker1.AverageRating.Should().Be(4.0);
        var worker2 = result.Single(r => r.WorkerId == 2);
        worker2.ReviewCount.Should().Be(1);
        worker2.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenNoReviews_ReturnsEmptyList()
    {
        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync([]);

        var result = await _sut.GetSummaryAsync();

        result.Should().BeEmpty();
    }

    // =========================
    // GET MINE
    // =========================

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallersReviews()
    {
        var appointment = CompletedAppointment();
        var review = new Review
        {
            Id = 1,
            AppointmentId = 1,
            Appointment = appointment,
            CustomerId = 1,
            Customer = appointment.Customer,
            WorkerId = 1,
            Worker = appointment.Worker,
            Rating = 5,
        };

        _reviewRepo
            .Setup(r => r.GetAllAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<Func<IQueryable<Review>, IOrderedQueryable<Review>>>(),
                It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync([review]);

        var result = await _sut.GetMineAsync(1);

        result.Should().HaveCount(1);
        result[0].AppointmentId.Should().Be(1);
    }

    // =========================
    // DELETE
    // =========================

    [Fact]
    public async Task Delete_WhenExists_RemovesAndReturnsSuccess()
    {
        var appointment = CompletedAppointment();
        var review = new Review
        {
            Id = 1,
            Appointment = appointment,
            Customer = appointment.Customer,
            Worker = appointment.Worker,
            Rating = 5,
        };

        _reviewRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync(review);

        var result = await _sut.Delete(1);

        result.Success.Should().BeTrue();
        _reviewRepo.Verify(r => r.Delete(
            It.Is<Review>(rv => rv.Id == 1),
            It.IsAny<Expression<Func<Review, object>>[]>()), Times.Once);
        _uow.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsSuccessWithNullDataAndNeverDeletes()
    {
        _reviewRepo
            .Setup(r => r.GetByIdAsync(99, It.IsAny<Expression<Func<Review, object>>[]>()))
            .ReturnsAsync((Review?)null);

        var result = await _sut.Delete(99);

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
        _reviewRepo.Verify(r => r.Delete(
            It.IsAny<Review>(),
            It.IsAny<Expression<Func<Review, object>>[]>()), Times.Never);
        _uow.Verify(u => u.SaveAsync(), Times.Never);
    }
}
