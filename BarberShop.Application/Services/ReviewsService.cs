using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BarberShop.Application.Services;

public class ReviewsService : BaseService, IReviewsService
{
    private static readonly ActivitySource _activitySource =
        new("BarberShop.ReviewsService");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ReviewsService> _logger;

    public ReviewsService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        INotificationPublisher notifications,
        ILogger<ReviewsService> logger) : base(redis, notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<ReviewResponseDTO>> Create(int customerId, ReviewRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateReview");
        span?.SetTag("review.appointmentId", dto.AppointmentId);

        var appointment = await _uow.Appointments.GetByIdAsync(
            dto.AppointmentId,
            a => a.Customer, a => a.Worker, a => a.Service);

        if (appointment == null)
        {
            _logger.LogWarning("Review creation failed — appointment {AppointmentId} not found", dto.AppointmentId);
            return Result<ReviewResponseDTO>.Fail("Appointment not found");
        }

        // Defense in depth: the controller already resolves customerId from the
        // caller's own JWT, but this project has a history of IDOR regressions
        // (see sprint items 1.1/1.2), so the service re-checks ownership itself
        // rather than trusting the caller blindly.
        if (appointment.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Review creation failed — customer {CustomerId} does not own appointment {AppointmentId}",
                customerId, dto.AppointmentId);
            return Result<ReviewResponseDTO>.Fail("You can only review your own appointments");
        }

        if (appointment.Status != Status.Completed)
        {
            _logger.LogWarning(
                "Review creation failed — appointment {AppointmentId} is not completed", dto.AppointmentId);
            return Result<ReviewResponseDTO>.Fail("Only completed appointments can be reviewed");
        }

        if (dto.Rating < 1 || dto.Rating > 5)
        {
            _logger.LogWarning("Review creation failed — rating {Rating} out of range", dto.Rating);
            return Result<ReviewResponseDTO>.Fail("Rating must be between 1 and 5");
        }

        var existing = await _uow.Reviews.GetAllAsync(r => r.AppointmentId == dto.AppointmentId);
        if (existing.Count > 0)
        {
            _logger.LogWarning(
                "Review creation failed — appointment {AppointmentId} already reviewed", dto.AppointmentId);
            return Result<ReviewResponseDTO>.Fail("This appointment has already been reviewed");
        }

        var review = new Review
        {
            AppointmentId = appointment.Id,
            CustomerId = appointment.CustomerId,
            WorkerId = appointment.WorkerId,
            Rating = dto.Rating,
            Comment = dto.Comment ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
        };

        await _uow.Reviews.AddAsync(review);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("reviews", "ReviewsChanged");

        _logger.LogInformation("Review {ReviewId} created for appointment {AppointmentId}", review.Id, appointment.Id);

        // Reuse the navigation props already loaded off the appointment instead
        // of a second round-trip just to satisfy the response mapping.
        review.Appointment = appointment;
        review.Customer = appointment.Customer;
        review.Worker = appointment.Worker;

        return Result<ReviewResponseDTO>.Ok(_mapper.Map<ReviewResponseDTO>(review));
    }

    // =========================
    // GET BY WORKER
    // =========================
    public async Task<List<ReviewResponseDTO>> GetByWorkerAsync(int workerId)
    {
        using var span = _activitySource.StartActivity("GetReviewsByWorker");
        span?.SetTag("worker.id", workerId);

        var result = await GetCachedAsync(
            $"reviews:worker:{workerId}",
            async () =>
            {
                var reviews = await _uow.Reviews.GetAllAsync(
                    r => r.WorkerId == workerId,
                    orderBy: q => q.OrderByDescending(r => r.CreatedAt),
                    includes: [r => r.Customer, r => r.Worker, r => r.Appointment.Service]);
                return _mapper.Map<List<ReviewResponseDTO>>(reviews);
            });

        return result ?? [];
    }

    // =========================
    // GET SUMMARY (all workers)
    // =========================
    public async Task<List<WorkerRatingSummaryDTO>> GetSummaryAsync()
    {
        using var span = _activitySource.StartActivity("GetReviewSummary");

        var result = await GetCachedAsync(
            "reviews:summary",
            async () =>
            {
                var reviews = await _uow.Reviews.GetAllAsync();
                return reviews
                    .GroupBy(r => r.WorkerId)
                    .Select(g => new WorkerRatingSummaryDTO
                    {
                        WorkerId = g.Key,
                        AverageRating = Math.Round(g.Average(r => r.Rating), 1),
                        ReviewCount = g.Count(),
                    })
                    .ToList();
            });

        return result ?? [];
    }

    // =========================
    // GET MINE
    // =========================
    public async Task<List<ReviewResponseDTO>> GetMineAsync(int customerId)
    {
        using var span = _activitySource.StartActivity("GetMyReviews");
        span?.SetTag("customer.id", customerId);

        var reviews = await _uow.Reviews.GetAllAsync(
            r => r.CustomerId == customerId,
            includes: [r => r.Customer, r => r.Worker, r => r.Appointment.Service]);

        return _mapper.Map<List<ReviewResponseDTO>>(reviews);
    }

    // =========================
    // GET ALL (admin moderation)
    // =========================
    public async Task<List<ReviewResponseDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllReviews");

        var reviews = await _uow.Reviews.GetAllAsync(
            orderBy: q => q.OrderByDescending(r => r.CreatedAt),
            includes: [r => r.Customer, r => r.Worker, r => r.Appointment.Service]);

        return _mapper.Map<List<ReviewResponseDTO>>(reviews);
    }

    // =========================
    // DELETE (admin moderation)
    // =========================
    public async Task<Result<ReviewResponseDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteReview");
        span?.SetTag("review.id", id);

        var review = await _uow.Reviews.GetByIdAsync(id);

        if (review == null)
        {
            _logger.LogWarning("Review {ReviewId} not found for deletion", id);
            return Result<ReviewResponseDTO>.Ok(null);
        }

        _uow.Reviews.Delete(review);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("reviews", "ReviewsChanged");

        _logger.LogInformation("Review {ReviewId} deleted", id);

        return Result<ReviewResponseDTO>.Ok(null);
    }
}
