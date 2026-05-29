namespace MineWatch.Api.DTOs;

public record ErrorResponse(string Message, int StatusCode, string? TraceId = null);
