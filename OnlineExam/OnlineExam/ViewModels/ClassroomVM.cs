using System;

namespace OnlineExam.ViewModels
{
    public class ClassroomVM
    {
        public int Id { get; set; }
        public string ClassName { get; set; }
        public string JoinCode { get; set; }
        public int StudentCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}