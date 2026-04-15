using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace OnlineExam.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        // Hàm này sẽ hứng đường dẫn /Student/Index
        public IActionResult Index()
        {
            return View();
        }
    }
}