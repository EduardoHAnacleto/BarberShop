using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;

namespace BarberShop.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // TraceId do OpenTelemetry — permite encontrar o trace no Grafana/Tempo
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Loga o erro com o TraceId para correlação no Grafana
        _logger.LogError(exception,
            "Unhandled exception. TraceId: {TraceId} | Path: {Path} | Method: {Method}",
            traceId,
            context.Request.Path,
            context.Request.Method);

        // Mapeia o tipo de exceção para o status HTTP correto
        var statusCode = exception switch
        {
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.Conflict,
            NotImplementedException => HttpStatusCode.NotImplemented,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = (int)statusCode,
            error = GetUserFriendlyMessage(exception, statusCode),
            traceId = traceId
        };

        await context.Response.WriteAsJsonAsync(response, cancellationToken);

        // Retorna true para indicar que a exceção foi tratada
        return true;
    }

    private static string GetUserFriendlyMessage(Exception exception, HttpStatusCode statusCode)
    {
        // Em produção nunca expõe detalhes internos
        // Apenas para exceções de negócio (BadRequest, NotFound) mostra a mensagem
        return statusCode switch
        {
            HttpStatusCode.BadRequest => exception.Message,
            HttpStatusCode.NotFound => exception.Message,
            HttpStatusCode.Conflict => exception.Message,
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.NotImplemented => "This feature is not implemented yet",
            _ => "An unexpected error occurred"
        };
    }
}
