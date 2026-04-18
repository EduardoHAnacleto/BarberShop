using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace BarberShop.Extensions;

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