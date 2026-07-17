using BarberShop.API.Extensions;
using BarberShop.API.Hubs;
using BarberShop.API.Middleware;
using BarberShop.API.Services;
using BarberShop.Application.Interfaces;
using BarberShop.Application.Services;
using BarberShop.Application.Validators;
using BarberShop.Infrastructure.Data;
using BarberShop.Infrastructure.Services;
using BarberShop.Infrastructure.UnitOfWork;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Controllers
// =========================
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// =========================
// AutoMapper
// =========================
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});

// =========================
// SignalR
// =========================
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// =========================
// Swagger
// =========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt(builder.Configuration);

// =========================
// Redis
// =========================
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")!;

var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;

var redisConnection = ConnectionMultiplexer.Connect(redisOptions);

builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);
builder.Services.AddSingleton<IRedisService, RedisService>();

// =========================
// Observabilidade
// =========================
builder.Services.AddObservability(builder.Configuration, redisConnection);

// =========================
// CORS
// =========================
// Allowed origins come from configuration (Cors:AllowedOrigins) so production
// domains can be added without a rebuild; the localhost dev origins are the
// fallback when nothing is configured.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins =
    [
        "http://localhost:3000",
        "http://localhost:3001",
        "http://127.0.0.1:3000",
        "http://127.0.0.1:3001",
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        // Specific origins with AllowCredentials so the browser will accept
        // SignalR WebSocket upgrades and authenticated XHR. AllowAnyOrigin
        // is incompatible with AllowCredentials per the CORS spec.
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// =========================
// Rate limiting
// =========================
// Protects the auth endpoints from brute-force and from the lockout-DoS where
// an attacker deliberately trips another user's 5-strike lockout. Keyed by
// remote IP with a fixed window. This is a secondary (network-level) guard —
// the primary defense against credential attacks is the per-account lockout
// (UserFailedLoginAttempts/UserLockoutEnd), which this limit does not affect.
// 30/min still blocks rapid-fire abuse while comfortably covering a full
// Playwright E2E run, where every browser project logs in independently from
// the same IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// =========================
// Database
// =========================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        }));
        // Default tracking — write flows (e.g. WorkersService.Create attaching
        // existing Services to a new Worker) require tracked entities so EF
        // does not mistake them for new rows and trigger IDENTITY_INSERT.
        // Read-heavy queries should opt into AsNoTracking() explicitly.
}

// =========================
// DI — Serviços
// =========================
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IServicesService, ServicesService>();
builder.Services.AddScoped<IAppointmentsService, AppointmentsService>();
builder.Services.AddScoped<ICustomersService, CustomersService>();
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IWorkersService, WorkersService>();
builder.Services.AddScoped<IWorkingHoursService, WorkingHoursService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISecurityStampService, SecurityStampService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IAppointmentAccessService, AppointmentAccessService>();
builder.Services.AddScoped<IReviewsService, ReviewsService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAppointmentReminderService, AppointmentReminderService>();
builder.Services.AddHostedService<AppointmentReminderBackgroundService>();
builder.Services.AddScoped<IWaitlistService, WaitlistService>();
builder.Services.AddScoped<IWorkerScheduleService, WorkerScheduleService>();
builder.Services.AddSingleton<IShopClock, ShopClock>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();

// =========================
// Health Checks
// =========================
var healthChecks = builder.Services.AddHealthChecks()
    .AddRedis(
        redisConnectionString,
        name: "redis",
        tags: ["cache", "infrastructure"]);

if (!builder.Environment.IsDevelopment())
{
    healthChecks.AddSqlServer(
        connectionString!,
        name: "sqlserver",
        tags: ["database", "infrastructure"]);
}

builder.Services.AddMemoryCache();

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// FluentValidation — validators are registered for manual use in service layer.
// Controllers already validate ModelState explicitly via if (!ModelState.IsValid).
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

// =========================
// Pipeline HTTP
// =========================
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseExceptionHandler();
app.MapPrometheusScrapingEndpoint();
app.UseCors("FrontendPolicy");
app.UseStaticFiles();
app.UseRateLimiter();

// Swagger UI is helpful for a portfolio demo, so it stays on by default. Set
// Swagger:Enabled=false to disable it in a hardened production deployment.
if (builder.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwaggerWithJwt();
}
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// =========================
// Health Check Endpoints
// =========================
app.MapHealthChecks("/health");

// The verbose per-dependency report is deliberately public on the portfolio
// demo (default true), but a rented client instance disables it via
// HealthChecks__DetailEnabled=false — it enumerates infrastructure internals
// (sprint12072026license §6). The plain /health above always stays available
// for the container healthcheck.
if (builder.Configuration.GetValue("HealthChecks:DetailEnabled", true))
{
    app.MapHealthChecks("/health/detail", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = new
            {
                status = report.Status.ToString(),
                duration = report.TotalDuration.TotalMilliseconds + "ms",
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds + "ms",
                    tags = e.Value.Tags,
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
            };

            await context.Response.WriteAsJsonAsync(result);
        }
    });
}

// =========================
// Controllers e Hubs
// =========================
try
{
    app.MapControllers();
}
catch (ReflectionTypeLoadException ex)
{
    foreach (var e in ex.LoaderExceptions)
        Console.WriteLine(e?.Message);
    throw;
}

app.MapHub<WorkersHub>("/workersHub").RequireCors("FrontendPolicy");
app.MapHub<ServicesHub>("/servicesHub").RequireCors("FrontendPolicy");
app.MapHub<CustomersHub>("/customersHub").RequireCors("FrontendPolicy");
app.MapHub<AppointmentsHub>("/appointmentsHub").RequireCors("FrontendPolicy");
app.MapHub<UsersHub>("/usersHub").RequireCors("FrontendPolicy");
app.MapHub<ReviewsHub>("/reviewsHub").RequireCors("FrontendPolicy");
app.MapHub<ScheduleHub>("/scheduleHub").RequireCors("FrontendPolicy");
app.MapHub<WorkerSchedulesHub>("/workerSchedulesHub").RequireCors("FrontendPolicy");

app.Run();
