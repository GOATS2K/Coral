using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PluginController : ControllerBase
    {
        private readonly IPluginContext _pluginContext;


        public PluginController(IPluginContext pluginContext)
        {
            _pluginContext = pluginContext;
        }

        [HttpGet]
        [Route("dynamic/{pluginName}/{route}")]
        public ActionResult RunPluginRoute(string pluginName, string route)
        {
            return _pluginContext.LoadRoute(pluginName, route);
        }

        [HttpGet]
        [Route("unload")]
        public ActionResult UnloadPlugins()
        {
            _pluginContext.UnloadAll();
            return Ok();
        }
    }
}
