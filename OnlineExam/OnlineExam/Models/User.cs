using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool? IsActive { get; set; }

    public virtual ICollection<ClassroomMember> ClassroomMembers { get; set; } = new List<ClassroomMember>();

    public virtual ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();

    public virtual ICollection<ExamPaper> ExamPapers { get; set; } = new List<ExamPaper>();

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
