using Microsoft.AspNetCore.Mvc;

namespace ProgKids.Notification.API.Controllers;

public class BotPanel : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}