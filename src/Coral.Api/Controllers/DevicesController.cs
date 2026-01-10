using System.Security.Claims;
using Coral.Dto.Auth;
using Coral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetDevices()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var deviceIdClaim = User.FindFirst("device_id")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        Guid? currentDeviceId = Guid.TryParse(deviceIdClaim, out var deviceId) ? deviceId : null;

        var devices = await _deviceService.GetUserDevicesAsync(userId, currentDeviceId);
        return Ok(devices);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var success = await _deviceService.DeleteDeviceAsync(userId, id);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
