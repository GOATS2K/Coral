using Coral.Configuration;
using Coral.Configuration.Models;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    [HttpGet]
    public ActionResult<ServerConfiguration> GetConfiguration()
    {
        var config = new ServerConfiguration();
        ApplicationConfiguration.GetConfiguration().Bind(config);
        return Ok(config);
    }

    [HttpPut]
    public IActionResult UpdateConfiguration([FromBody] ServerConfiguration config)
    {
        try
        {
            ApplicationConfiguration.WriteConfiguration(config);
            return Ok(new { message = "Configuration updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to update configuration: {ex.Message}" });
        }
    }
}
