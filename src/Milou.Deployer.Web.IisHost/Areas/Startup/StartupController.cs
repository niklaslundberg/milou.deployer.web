﻿using Microsoft.AspNetCore.Mvc;
using Milou.Deployer.Web.Core.Startup;

namespace Milou.Deployer.Web.IisHost.Areas.Startup
{
    public class StartupController : Controller
    {
        [Route("~/startup")]
        [HttpGet]
        public IActionResult Index([FromServices] StartupTaskContext startupTaskContext)
        {
            if (startupTaskContext.IsCompleted)
            {
                return Redirect("/");
            }

            return View();
        }
    }
}
