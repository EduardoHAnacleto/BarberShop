using BarberShop.API.Extensions;
using BarberShop.API.Hubs;
using BarberShop.API.Middleware;
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
builder.Services.AddDirectoryBrowser();

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
//builder.Services.AddSwaggerGen();
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        // Specific origins with AllowCredentials so the browser will accept
        // SignalR WebSocket upgrades and authenticated XHR. AllowAnyOrigin
        // is incompatible with AllowCredentials per the CORS spec.
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:3001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
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
builder.Services.AddSingleton<TokenService>();

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

//Fluent Validation
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

// =========================
// Pipeline HTTP
// =========================
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();
app.UseExceptionHandler();
app.MapPrometheusScrapingEndpoint();
app.UseCors("FrontendPolicy");
app.UseStaticFiles();

//app.UseSwagger();
//app.UseSwaggerUI();
app.UseSwaggerWithJwt();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwaggerWithJwt();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// =========================
// Health Check Endpoints
// =========================

app.MapHealthChecks("/health");

// Endpoint
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

//Hubs
app.MapHub<WorkersHub>("/workersHub").RequireCors("FrontendPolicy");
app.MapHub<ServicesHub>("/servicesHub").RequireCors("FrontendPolicy");
app.MapHub<CustomersHub>("/customersHub").RequireCors("FrontendPolicy");
app.MapHub<AppointmentsHub>("/appointmentsHub").RequireCors("FrontendPolicy");
app.MapHub<UsersHub>("/usersHub").RequireCors("FrontendPolicy");

app.Run();