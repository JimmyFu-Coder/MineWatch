using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.AlertEngine;

public interface IAlertEngine
{
    Task EvaluateAsync(TelemetryReading reading);
}
