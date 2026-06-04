using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineWatch.Api.DTOs;
using MineWatch.Api.Services;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController(IAlertService alertService) : ControllerBase
{
    // Rule CRUD
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (items, total) = await alertService.GetRulesAsync(page, pageSize);
        return Ok(new PageResponse<RuleResponse>(items, total, page, pageSize));
    }

    [HttpGet("rules/{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id)
    {
        var rule = await alertService.GetRuleAsync(id);
        return rule == null ? NotFound() : Ok(rule);
    }

    [HttpPost("rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request)
    {
        var rule = await alertService.CreateRuleAsync(request);
        return CreatedAtAction("GetRule", new { id = rule.Id }, rule);
    }

    [HttpPut("rules/{id:guid}")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request)
    {
        var rule = await alertService.UpdateRuleAsync(id, request);
        return rule == null ? NotFound() : Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var success = await alertService.DeleteRuleAsync(id);
        return success ? NoContent() : NotFound();
    }

    // Alert operations
    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] AlertStatus? status = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? ruleId = null)
    {
        var (items, total) = await alertService.GetAlertsAsync(page, pageSize, status, deviceId, ruleId);
        return Ok(new PageResponse<AlertResponse>(items, total, page, pageSize));
    }

    [HttpPut("{id:guid}/acknowledge")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, [FromBody] AcknowledgeRequest request)
    {
        var alert = await alertService.AcknowledgeAlertAsync(id, request);
        return alert == null ? NotFound() : Ok(alert);
    }

    [HttpPut("{id:guid}/resolve")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> ResolveAlert(Guid id)
    {
        var alert = await alertService.ResolveAlertAsync(id);
        return alert == null ? NotFound() : Ok(alert);
    }
}
