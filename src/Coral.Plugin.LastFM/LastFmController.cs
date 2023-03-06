using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coral.PluginBase;

namespace Coral.Plugin.LastFM
{
    public class LastFmController : PluginBaseController
    {
        private readonly ILastFmService _lastFmService;

        public LastFmController(ILastFmService lastFmService)
        {
            _lastFmService = lastFmService;
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
