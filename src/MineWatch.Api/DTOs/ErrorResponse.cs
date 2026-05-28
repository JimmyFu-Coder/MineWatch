namespace MineWatch.Api.DTOs;

public record ErrorResponse(string Message, int code, string? TraceId = null);