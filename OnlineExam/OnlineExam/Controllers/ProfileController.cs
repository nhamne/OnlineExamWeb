using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineExam.Models;
using OnlineExam.ViewModels;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OnlineExam.Controllers
{
    [Authorize] // Available for both Student and Teacher
    public class ProfileController : Controller
    {
        private readonly OnlineExamDbContext _context;

        public ProfileController(OnlineExamDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var vm = new UserProfileVM
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(UserProfileVM model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ.";
                return RedirectToAction("Index");
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            user.FullName = model.FullName.Trim();
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDTO model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu mật khẩu không hợp lệ.";
                return RedirectToAction("Index");
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Verify old password
            var passwordHasher = new PasswordHasher<User>();
            PasswordVerificationResult verificationResult = PasswordVerificationResult.Failed;

            try
            {
                verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
            }
            catch (FormatException)
            {
                // Fallback if password is not hashed
                if (user.PasswordHash == model.CurrentPassword)
                {
                    verificationResult = PasswordVerificationResult.Success;
                }
            }

            if (verificationResult == PasswordVerificationResult.Failed && user.PasswordHash != model.CurrentPassword) // check for cleartext fallback just in case
            {
                TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction("Index");
            }

            // Hash new password
            user.PasswordHash = passwordHasher.HashPassword(user, model.NewPassword);
            
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index");
        }
    }
}
