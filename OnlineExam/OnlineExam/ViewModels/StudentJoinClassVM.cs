using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels
{
    public class StudentJoinClassVM
    {
        public string StudentName { get; set; }
        public List<StudentJoinedClassItemVM> JoinedClasses { get; set; } = new List<StudentJoinedClassItemVM>();
    }

    public class StudentJoinedClassItemVM
    {
        public int ClassroomId { get; set; }
        public string ClassName { get; set; }
        public string TeacherName { get; set; }
        public string JoinCode { get; set; }
        public DateTime? JoinedAt { get; set; }
        public int ExamCount { get; set; }
        public List<StudentJoinedClassExamVM> RecentExams { get; set; } = new List<StudentJoinedClassExamVM>();
    }

    public class StudentJoinedClassExamVM
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
    }
}