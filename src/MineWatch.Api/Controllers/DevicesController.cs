using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineWatch.Api.DTOs;
using MineWatch.Api.Services;

namespace MineWatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController(IDeviceService deviceService) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (items, total) = await deviceService.GetAllAsync(page, pageSize);
        var response = new PageResponse<DeviceResponse>(
            items.Select(d => new DeviceResponse(d.Id, d.Name, d.Type, d.Status, d.CreatedAt, d.UpdatedAt)).ToList(),
            total, page, pageSize);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var device = await deviceService.GetByIdAsync(id);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateDeviceRequest request)
    {
        var device = await deviceService.CreateAsync(request);
        return CreatedAtAction("GetById", new { id = device.Id }, device);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var device = await deviceService.UpdateAsync(id, request);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        var success = await deviceService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }
}
