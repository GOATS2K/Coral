using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Plugin.LastFM
{
    [Route("api/plugin/[controller]")]
    [ApiController]
    public class LastFmController : ControllerBase
    {
        private readonly ILastFmService _lastFmService;

        public LastFmController(ILastFmService lastFmService)
        {
            _lastFmService = lastFmService;
        }

        [HttpGet]
        public ActionResult Test()
        {
            return Ok(_lastFmService.HelloWorld());
        }
    }
}
