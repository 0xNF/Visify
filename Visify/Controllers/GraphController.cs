using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Visify.Controllers
{
    
    [Authorize]
    public class GraphController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}