using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace BarberShop.API.Extensions;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IConnectionMultiplexer redis)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: "BarberShop.API",
                serviceVersion: "1.0.0");

        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
            ?? "http://localhost:4317";

        services.AddOpenTelemetry()

            // ========================
            // TRACES
            // ========================
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)

                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = ctx =>
                        !ctx.Request.Path.StartsWithSegments("/metrics");
                })

                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation(redis, options =>
                {
                    options.SetVerboseDatabaseStatements = true;
                })

                .AddSource("BarberShop.AppointmentsService")
                .AddSource("BarberShop.WorkersService")
                .AddSource("BarberShop.CustomersService")
                .AddSource("BarberShop.ServicesService")
                .AddSource("BarberShop.UsersService")
                .AddSource("BarberShop.WorkingHoursService")
                .AddSource("BarberShop.AuthService")

                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }))

            // ========================
            // METRICS
            // ========================
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()


                .AddMeter("BarberShop.AppointmentsService")
                .AddMeter("BarberShop.WorkersService")
                .AddMeter("BarberShop.UsersService")
                .AddMeter("BarberShop.ServicesService")
                .AddMeter("BarberShop.CustomersService")
                .AddMeter("BarberShop.AuthService")

                .AddPrometheusExporter()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }))

            // ========================
            // LOGS
            // ========================
            .WithLogging(logging => logging
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }));

        return services;
    }
}