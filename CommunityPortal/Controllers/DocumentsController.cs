using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class DocumentsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
