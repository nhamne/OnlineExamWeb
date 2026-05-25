using System.Collections.Generic;

namespace OnlineExam.ViewModels
{
    public class TeacherReportVM
    {
        public int TotalStudents { get; set; }
        public int TotalSubmissions { get; set; }
        public double AverageScore { get; set; }
        
        // Distribution
        public int Score0To4 { get; set; }
        public int Score4To6 { get; set; }
        public int Score6To8 { get; set; }
        public int Score8To10 { get; set; }

        public List<SessionPerformanceVM> RecentPerformances { get; set; } = new List<SessionPerformanceVM>();
    }

    public class SessionPerformanceVM
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int TotalSubmissions { get; set; }
        public double AverageScore { get; set; }
    }
}
