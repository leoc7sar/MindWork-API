using System.Diagnostics;

namespace MindWork.Api.Middleware;

/// <summary>
/// Middleware simples de tracing para rastrear cada requisição HTTP:
/// - Gera/propaga um CorrelationId
/// - Mede o tempo de execução
/// - Escreve informações no log
/// Isso ajuda a atender o requisito de "Tracing" da disciplina de .NET.
/// </summary>
public class RequestTracingMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTracingMiddleware> _logger;

    public RequestTracingMiddleware(RequestDelegate next, ILogger<RequestTracingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeader] = correlationId;
        }

        context.Response.Headers[CorrelationIdHeader] = correlationId!;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Request started {Method} {Path} CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            await _next(context);

            stopwatch.Stop();

            _logger.LogInformation(
                "Request finished {Method} {Path} CorrelationId={CorrelationId} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Request error {Method} {Path} CorrelationId={CorrelationId} ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
