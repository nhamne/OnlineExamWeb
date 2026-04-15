using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using OnlineExam.ViewModels;
using OnlineExam.Models;

namespace OnlineExam.Controllers
{
    public class AuthController : Controller
    {
        private readonly OnlineExamDbContext _context;

        public AuthController(OnlineExamDbContext context)
        {
            _context = context;
        }

        private static string NormalizeRole(string? role)
        {
            return string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase) ? "teacher" : "student";
        }

        private static bool VerifyPassword(string enteredPassword, string storedPassword)
        {
            if (string.Equals(storedPassword, enteredPassword, StringComparison.Ordinal))
            {
                return true;
            }

            var passwordHasher = new PasswordHasher<User>();
            var verificationResult = passwordHasher.VerifyHashedPassword(new User(), storedPassword, enteredPassword);
            return verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded;
        }

        private AuthPageViewModel BuildAuthPageViewModel(string? role, bool isRegister, string? errorMessage = null)
        {
            var normalizedRole = NormalizeRole(role);
            var isTeacher = normalizedRole == "teacher";

            return new AuthPageViewModel
            {
                Role = normalizedRole,
                IsRegister = isRegister,
                PageTitle = isRegister ? "Đăng ký" : "Đăng nhập",
                HeroTagline = isTeacher ? "Teacher Workspace" : "Student Workspace",
                HeroTitle = isTeacher ? "Không gian giảng dạy hiện đại" : "Không gian học tập tập trung",
                HeroDescription = isTeacher
                    ? "Tạo đề thi, quản lý lớp học và theo dõi tiến trình học sinh trong một hệ thống duy nhất."
                    : "Làm bài thi, theo dõi tiến độ và phát triển năng lực học tập mỗi ngày.",
                FormTitle = isRegister
                    ? (isTeacher ? "Tạo tài khoản giáo viên" : "Tạo tài khoản học sinh")
                    : (isTeacher ? "Giáo viên đăng nhập" : "Học sinh đăng nhập"),
                FormDescription = isRegister
                    ? "Hoàn tất thông tin để bắt đầu sử dụng hệ thống."
                    : "Đăng nhập để tiếp tục phiên làm việc của bạn.",
                SubmitLabel = isRegister ? "Tạo tài khoản" : "Đăng nhập",
                AlternatePrompt = isRegister ? "Đã có tài khoản?" : "Chưa có tài khoản?",
                AlternateActionLabel = isRegister ? "Đăng nhập ngay" : "Đăng ký ngay",
                ErrorMessage = errorMessage
            };
        }

        // 1. GET: Hiển thị giao diện (dùng _AuthLayout)
        [HttpGet]
        public IActionResult Login(string role = "student")
        {
            // Nếu đã đăng nhập rồi thì không cho vào trang Login nữa, đá vào trong luôn
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Teacher")) return RedirectToAction("Index", "Teacher");
                return RedirectToAction("Index", "Student");
            }
            return View("AuthForm", BuildAuthPageViewModel(role, isRegister: false));
        }

        // 2. POST: Xử lý đăng nhập và CHUYỂN TRANG
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string role = "student", bool rememberMe = false)
        {
            var normalizedRole = NormalizeRole(role);
            var normalizedEmail = email.Trim();

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);

            if (user == null || user.IsActive == false || !VerifyPassword(password, user.PasswordHash))
            {
                return View("AuthForm", BuildAuthPageViewModel(normalizedRole, isRegister: false, errorMessage: "Sai email hoặc mật khẩu!"));
            }

            var roleFromDB = NormalizeRole(user.Role);

            if (!string.Equals(roleFromDB, normalizedRole, StringComparison.OrdinalIgnoreCase))
            {
                return View("AuthForm", BuildAuthPageViewModel(normalizedRole, isRegister: false, errorMessage: $"Tài khoản này là tài khoản {(roleFromDB == "teacher" ? "giáo viên" : "học sinh")}, vui lòng chọn đúng tab role."));
            }

            // === TẠO COOKIE ĐĂNG NHẬP (LƯU VÀO TRÌNH DUYỆT) ===
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleFromDB == "teacher" ? "Teacher" : "Student")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Lệnh này chính thức ghi nhận user đã đăng nhập
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = rememberMe });

            // === ĐIỀU HƯỚNG SANG LAYOUT CHÍNH CHỨA MENU ===
            if (string.Equals(roleFromDB, "teacher", StringComparison.OrdinalIgnoreCase))
            {
                // Đá sang hàm Index của TeacherController
                return RedirectToAction("Index", "Teacher");
            }
            else
            {
                // Đá sang hàm Index của StudentController
                return RedirectToAction("Index", "Student");
            }
        }

        // GET: /Auth/Register

        // Hàm này được gọi khi người dùng gõ URL truy cập trang đăng ký

        [HttpGet]
        public IActionResult Register(string role = "student")
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Teacher")) return RedirectToAction("Index", "Teacher");
                return RedirectToAction("Index", "Student");
            }

            return View("AuthForm", BuildAuthPageViewModel(role, isRegister: true));

        }

        [HttpPost]
        public async Task<IActionResult> Register(
            string fullname,
            string email,
            string password,
            string confirmPassword,
            string role = "student")
        {
            var normalizedRole = NormalizeRole(role);
            var normalizedEmail = email.Trim();

            if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return View("AuthForm", BuildAuthPageViewModel(normalizedRole, isRegister: true, errorMessage: "Vui lòng nhập đầy đủ thông tin bắt buộc."));
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                return View("AuthForm", BuildAuthPageViewModel(normalizedRole, isRegister: true, errorMessage: "Mật khẩu xác nhận không khớp."));
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);

            if (existingUser != null)
            {
                return View("AuthForm", BuildAuthPageViewModel(normalizedRole, isRegister: true, errorMessage: "Email này đã được sử dụng."));
            }

            var roleFromDB = normalizedRole == "teacher" ? "Teacher" : "Student";
            var newUser = new User
            {
                FullName = fullname.Trim(),
                Email = normalizedEmail,
                Role = roleFromDB,
                IsActive = true
            };

            var passwordHasher = new PasswordHasher<User>();
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, newUser.FullName),
                new Claim(ClaimTypes.Email, newUser.Email),
                new Claim(ClaimTypes.Role, roleFromDB)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return roleFromDB == "Teacher"
                ? RedirectToAction("Index", "Teacher")
                : RedirectToAction("Index", "Student");
        }

        // Hàm Đăng xuất
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }
    }
}