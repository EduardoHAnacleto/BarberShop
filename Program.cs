using AutoMapper;
using BarberShop.Data;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories;
using BarberShop.Repositories.Interfaces;
using BarberShop.Services;
using BarberShop.Services.Interfaces;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Controllers
builder.Services.AddControllers()
      .AddJsonOptions(o =>
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddDirectoryBrowser();

//AutoMapper DTO - Model
builder.Services.AddAutoMapper(typeof(Program));

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// DI
builder.Services.AddScoped<IAppointmentsService, AppointmentsService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = ConfigurationOptions.Parse(
        builder.Configuration.GetConnectionString("Redis")!
    );
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<RedisService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            //.WithOrigins(
            //    "",
            //    ""
           // )
            .AllowAnyHeader()
            .AllowAnyOrigin()
            .AllowAnyMethod();
    });
});

// Database
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

//Environment Set To Development
builder.Environment.IsDevelopment();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(
            connectionString,
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null
                );
                sqlOptions.CommandTimeout(60);
            }
        ).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    );
}

// Business Hours Schedule settings
builder.Services.Configure<BusinessHoursSettings>(
    builder.Configuration.GetSection("BusinessHours")
);


builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

try
{
    app.MapControllers();
}
catch (ReflectionTypeLoadException ex)
{
    foreach (var e in ex.LoaderExceptions)
    {
        Console.WriteLine(e?.Message);
    }
    throw;
}

app.MapHub<WorkersHub>("/workersHub")
    .RequireCors("FrontendPolicy");
app.MapHub<ServicesHub>("/servicesHub")
    .RequireCors("FrontendPolicy");
app.MapHub<CustomersHub>("/customersHub")
    .RequireCors("FrontendPolicy");
app.MapHub<AppointmentsHub>("/appointmentsHub")
    .RequireCors("FrontendPolicy");
app.MapHub<UsersHub>("/usersHub")
    .RequireCors("FrontendPolicy");

app.Run();


