using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OnlineExam.ViewModels;
using System;
using System.Collections.Generic;

namespace OnlineExam.Controllers // Chú ý: Đổi tên namespace cho khớp với project của bạn
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        // Hàm này sẽ bắt đường dẫn /Teacher/Index
        public IActionResult Index()
        {
            // Dữ liệu mẫu truyền sang view để khắc phục lỗi Null Reference
            var dashboardData = new TeacherDashboardVM()
            {
                TeacherName = "Tên Giảng Viên",
                TotalClasses = 10,
                TotalExams = 8,
                OngoingSessions = 2,
                UpcomingSessions = 4,
                RecentSessions = new List<SessionItemVM>
                {
                    new SessionItemVM
                    {
                        SessionName = "Kiểm tra 15p",
                        ClassName = "Lớp 10A1",
                        StartTime = DateTime.Now.AddHours(-1),
                        EndTime = DateTime.Now.AddHours(1)
                    }
                }
            };

            // Trả về phần ruột kèm theo dữ liệu Model
            return View(dashboardData);
        }
    }
}