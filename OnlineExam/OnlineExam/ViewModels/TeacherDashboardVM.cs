using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels // Đảm bảo namespace này khớp với file Index.cshtml của bạn
{
    // Class chứa tổng hợp các con số thống kê cho màn hình Dashboard
    public class TeacherDashboardVM
    {
        public string TeacherName { get; set; } = string.Empty;
        public int TotalClasses { get; set; }
        public int TotalExams { get; set; }
        public int OngoingSessions { get; set; }
        public int UpcomingSessions { get; set; }
        public int SessionsToday { get; set; }

        // Danh sách các ca thi để in ra bảng
        public List<SessionItemVM> RecentSessions { get; set; } = new List<SessionItemVM>();
    }

    // Class chứa thông tin của từng dòng ca thi trong bảng
    public class SessionItemVM
    {
        public string SessionName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Thuộc tính tự động tính toán trạng thái dựa vào giờ hệ thống hiện tại
        public string Status
        {
            get
            {
                var now = DateTime.Now;
                if (now < StartTime) return "Sắp diễn ra";
                if (now >= StartTime && now <= EndTime) return "Đang diễn ra";
                return "Đã kết thúc";
            }
        }
    }
}