using AutoMapper;
using BarberShop.Application.Common;
using BarberShop.Application.DTOs;
using BarberShop.Application.Interfaces;
using BarberShop.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BarberShop.Application.Services;

public class UsersService : BaseService, IUsersService
{
    // =========================
    // OBSERVABILITY
    // =========================
    private static readonly ActivitySource _activitySource =
        new("BarberShop.UsersService");

    private static readonly Meter _meter =
        new("BarberShop.UsersService");

    private static readonly Counter<long> _usersCreated =
        _meter.CreateCounter<long>(
            "barbershop.users.created",
            description: "Total number of users created");

    private static readonly Counter<long> _usersDeleted =
        _meter.CreateCounter<long>(
            "barbershop.users.deleted",
            description: "Total number of users deleted");

    private static readonly Histogram<double> _operationDuration =
        _meter.CreateHistogram<double>(
            "barbershop.users.operation_duration",
            unit: "ms",
            description: "Duration of user operations");

    // =========================
    // DEPENDENCIES
    // =========================
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<UsersService> _logger;
    private readonly ISecurityStampService _stamps;

    public UsersService(
        IUnitOfWork uow,
        IMapper mapper,
        IRedisService redis,
        INotificationPublisher notifications,
        ILogger<UsersService> logger,
        ISecurityStampService stamps) : base(redis, notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _stamps = stamps;
    }

    // =========================
    // GET ALL
    // =========================
    public async Task<List<UserResponseDTO>> GetAllAsync()
    {
        using var span = _activitySource.StartActivity("GetAllUsers");

        _logger.LogInformation("Fetching all users");

        var result = await GetCachedAsync(
            "users:all",
            async () => _mapper.Map<List<UserResponseDTO>>(await _uow.Users.GetAllAsync())
        );

        var count = result?.Count ?? 0;
        span?.SetTag("users.count", count);
        _logger.LogInformation("Fetched {Count} users", count);

        return result ?? [];
    }

    // =========================
    // GET ALL PAGED
    // =========================
    public async Task<PagedResult<UserResponseDTO>> GetAllAsync(PaginationParams pagination)
    {
        using var span = _activitySource.StartActivity("GetAllUsersPaged");
        span?.SetTag("pagination.page", pagination.Page);
        span?.SetTag("pagination.pageSize", pagination.PageSize);

        _logger.LogInformation(
            "Fetching users page {Page} (size {PageSize})",
            pagination.Page, pagination.PageSize);

        var paged = await _uow.Users.GetPagedAsync(pagination);

        var mapped = PagedResult<UserResponseDTO>.Create(
            _mapper.Map<List<UserResponseDTO>>(paged.Items),
            paged.TotalCount,
            pagination);

        span?.SetTag("users.totalCount", mapped.TotalCount);

        _logger.LogInformation(
            "Fetched page {Page}/{TotalPages} ({TotalCount} total users)",
            mapped.Page, mapped.TotalPages, mapped.TotalCount);

        return mapped;
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<UserResponseDTO?> GetByIdAsync(int id)
    {
        using var span = _activitySource.StartActivity("GetUserById");
        span?.SetTag("user.id", id);

        _logger.LogInformation("Fetching user {UserId}", id);

        var result = await GetCachedAsync(
            $"users:{id}",
            async () =>
            {
                var user = await _uow.Users.GetByIdAsync(id);
                return user == null ? null : _mapper.Map<UserResponseDTO>(user);
            }
        );

        if (result == null)
            _logger.LogWarning("User {UserId} not found", id);

        return result;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<Result<UserResponseDTO>> Create(UserRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("CreateUser");
        span?.SetTag("user.email", dto.Email);
        span?.SetTag("user.role", dto.UserRole.ToString());

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating user with email {Email} and role {Role}",
            dto.Email, dto.UserRole);

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            _logger.LogWarning("User creation failed — invalid email");
            return Result<UserResponseDTO>.Fail("Invalid Email");
        }

        var exists = await _uow.Users.GetAllAsync(u => u.Email == dto.Email);

        if (exists.Any())
        {
            _logger.LogWarning(
                "User creation failed — email already exists: {Email}", dto.Email);
            return Result<UserResponseDTO>.Fail("Email already exists");
        }

        var user = _mapper.Map<User>(dto);

        await _uow.Users.AddAsync(user);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("users", "UsersChanged");

        stopwatch.Stop();

        span?.SetTag("user.id", user.Id);
        _usersCreated.Add(1,
            new TagList { { "role", dto.UserRole.ToString() } });
        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "create" } });

        _logger.LogInformation(
            "User {UserId} ({Email}) created in {ElapsedMs}ms",
            user.Id, user.Email, stopwatch.Elapsed.TotalMilliseconds);

        return Result<UserResponseDTO>.Ok(_mapper.Map<UserResponseDTO>(user));
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<Result<UserResponseDTO>> Update(int id, UserRequestDTO dto)
    {
        using var span = _activitySource.StartActivity("UpdateUser");
        span?.SetTag("user.id", id);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Updating user {UserId}", id);

        var user = await _uow.Users.GetByIdAsync(id);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for update", id);
            return Result<UserResponseDTO>.Ok(null);
        }

        // Credential changes must revoke every previously issued JWT: rotate
        // the SecurityStamp when the login email changes or a new password is
        // provided (empty PasswordHash means "keep current password").
        var emailChanged = !string.Equals(
            user.Email, dto.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
        var passwordChanged = !string.IsNullOrWhiteSpace(dto.PasswordHash);

        _mapper.Map(dto, user);

        if (emailChanged || passwordChanged)
            user.SecurityStamp = Guid.NewGuid().ToString("N");

        _uow.Users.Update(user);
        await _uow.SaveAsync();

        if (emailChanged || passwordChanged)
        {
            await _stamps.InvalidateCacheAsync(id);
            _logger.LogInformation(
                "User {UserId} credentials changed — tokens revoked", id);
        }

        await InvalidateAndNotifyAsync("users", "UsersChanged");

        stopwatch.Stop();

        _operationDuration.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new TagList { { "operation", "update" } });

        _logger.LogInformation(
            "User {UserId} updated in {ElapsedMs}ms",
            id, stopwatch.Elapsed.TotalMilliseconds);

        return Result<UserResponseDTO>.Ok(_mapper.Map<UserResponseDTO>(user));
    }

    // =========================
    // DELETE
    // =========================
    public async Task<Result<UserResponseDTO>> Delete(int id)
    {
        using var span = _activitySource.StartActivity("DeleteUser");
        span?.SetTag("user.id", id);

        _logger.LogInformation("Deleting user {UserId}", id);

        var user = await _uow.Users.GetByIdAsync(id);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for deletion", id);
            return Result<UserResponseDTO>.Ok(null);
        }

        _uow.Users.Delete(user);
        await _uow.SaveAsync();
        await InvalidateAndNotifyAsync("users", "UsersChanged");

        _usersDeleted.Add(1);

        _logger.LogInformation("User {UserId} deleted successfully", id);

        return Result<UserResponseDTO>.Ok(null);
    }
}
