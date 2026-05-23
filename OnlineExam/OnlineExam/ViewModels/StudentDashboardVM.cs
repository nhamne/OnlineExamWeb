using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels
{
    public class StudentDashboardVM
    {
        public string StudentName { get; set; }
        public int PendingExamsCount { get; set; }
        public List<StudentExamItemVM> Exams { get; set; } = new List<StudentExamItemVM>();
        public List<ClassroomVM> JoinedClasses { get; set; } = new List<ClassroomVM>();
        public List<StudentScoreChartItemVM> ScoreHistory { get; set; } = new List<StudentScoreChartItemVM>();
    }

    public class StudentResultsVM
    {
        public string StudentName { get; set; }
        public List<OnlineExam.Models.Submission> Submissions { get; set; } = new List<OnlineExam.Models.Submission>();
        public List<StudentScoreChartItemVM> ScoreHistory { get; set; } = new List<StudentScoreChartItemVM>();
    }

    public class StudentScoreChartItemVM
    {
        public string ExamName { get; set; }
        public double Score { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class ClassroomVM
    {
        public int Id { get; set; }
        public string ClassName { get; set; }
    }

    public class StudentExamItemVM
    {
        public int ExamSessionId { get; set; }
        public string Title { get; set; }
        public string TeacherName { get; set; }
        public int Duration { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public string ClassName { get; set; }
        public int ClassroomId { get; set; }
        public bool IsSubmitted { get; set; }

        public string Status
        {
            get
            {
                if (IsSubmitted) return "Đã nộp";
                var now = DateTime.Now;
                if (now > EndTime) return "Đã đóng";
                if (now >= StartTime && now <= EndTime)
                {
                    if ((EndTime - now).TotalHours <= 24) return "Sắp hết hạn";
                    return "Chưa làm";
                }
                return "Chưa mở";
            }
        }
    }
}
