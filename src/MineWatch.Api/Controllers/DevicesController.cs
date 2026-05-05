using Microsoft.AspNetCore.Mvc;
using MineWatch.Api.DTOs;
using MineWatch.Api.Services;

namespace MineWatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController(IDeviceService deviceService) : ControllerBase
{   
    
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (items, total) = await deviceService.GetAllAsync(page, pageSize);
        return Ok(new { items, total });
    }
    
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var device = await deviceService.GetByIdAsync(id);
        return device == null ? NotFound() : Ok(device);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateDeviceRequest request)
    {
        var device = await deviceService.CreateAsync(request);
        return CreatedAtAction("GetById", new { id = device.Id }, device);
    }
    
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var device = await deviceService.UpdateAsync(id, request);
        return device == null ? NotFound() : Ok(device);
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        var success = await deviceService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }
}