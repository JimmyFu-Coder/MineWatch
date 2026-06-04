using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MineWatch.Api.DTOs;
using MineWatch.Infrastructure.Data;

namespace MineWatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelemetryController(MineWatchDbContext context) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery] string? vehicleNo)
    {
        // Subquery: latest timestamp per device, then join to get full row.
        // Correctly handles any number of devices without a fixed row cap.
        var latestTimestamps = context.TelemetryReadings
            .GroupBy(r => r.DeviceId)
            .Select(g => new { DeviceId = g.Key, MaxTs = g.Max(r => r.Timestamp) });

        var query = context.TelemetryReadings
            .Join(latestTimestamps,
                r => new { r.DeviceId, r.Timestamp },
                lt => new { DeviceId = lt.DeviceId, Timestamp = lt.MaxTs },
                (r, _) => r);

        if (!string.IsNullOrWhiteSpace(vehicleNo))
            query = query.Where(r => r.VehicleNo == vehicleNo);

        var readings = await query.ToListAsync();

        var latest = readings
            .Select(r => new LatestPositionResponse(
                r.DeviceId, r.VehicleNo, r.Lat, r.Lon, r.Speed, r.Heading, r.Timestamp))
            .ToList();

        return Ok(latest);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string vehicleNo,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (string.IsNullOrWhiteSpace(vehicleNo))
            return BadRequest("vehicleNo is required");

        var query = context.TelemetryReadings
            .Where(r => r.VehicleNo == vehicleNo);

        if (from.HasValue)
            query = query.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.Timestamp <= to.Value);

        var total = await query.CountAsync();
        var points = await query
            .OrderBy(r => r.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new HistoryPoint(r.Lat, r.Lon, r.Speed, r.Heading, r.Timestamp))
            .ToListAsync();

        return Ok(new HistoryResponse(vehicleNo, points, total));
    }
}
