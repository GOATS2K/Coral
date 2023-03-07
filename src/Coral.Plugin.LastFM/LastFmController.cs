using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coral.PluginBase;
using Coral.PluginHost;

namespace Coral.Plugin.LastFM
{
    public class LastFmController : PluginBaseController
    {
        private readonly ILastFmService _lastFmService;

        public LastFmController(IServiceProxy serviceProxy)
        {
            _lastFmService = serviceProxy.GetService<ILastFmService>();
        }

        [HttpGet]
        [Route("authorize")]
        public ActionResult AuthorizeUser()
        {
            var apiKey = _lastFmService.GetApiKey();
            return Redirect($"https://last.fm/api/auth?api_key={apiKey}&cb={Request.Scheme}://{Request.Host}/api/plugin/lastfm/setToken");
        }

        [HttpGet]
        [Route("setToken")]
        public ActionResult SetUserToken([FromQuery] string token)
        {
            _lastFmService.SetUserToken(token);
            return Ok();
        }

        [HttpGet]
        [Route("guid")]
        public ActionResult SomethingNew()
        {
            return Ok(Guid.NewGuid().ToString());
        }

        [HttpGet]
        public ActionResult Test()
        {
            return Ok(_lastFmService.HelloWorld());
        }
    }
}
