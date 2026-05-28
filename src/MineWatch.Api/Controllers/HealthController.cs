using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Data;

namespace MineWatch.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly MineWatchDbContext _db;

    public HealthController(MineWatchDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Live()
    {
        return Ok(new { status = "healthy" });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var health = new { status = "unhealthy", database = "unknown" };

        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            health = new { status = canConnect ? "healthy" : "unhealthy", database = canConnect ? "connected" : "disconnected" };
        }
        catch
        {
            health = new { status = "unhealthy", database = "error" };
        }

        return health.status == "healthy" ? Ok(health) : StatusCode(503, health);
    }
}
