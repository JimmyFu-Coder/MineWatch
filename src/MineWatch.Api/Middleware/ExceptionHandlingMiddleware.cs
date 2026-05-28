using System.Diagnostics;
using MineWatch.Api.DTOs;

namespace MineWatch.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception caught in middleware");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("An error occurred", context.Response.StatusCode, traceId));
        }
    }
}