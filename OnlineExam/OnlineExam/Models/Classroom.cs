using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class Classroom
{
    public int Id { get; set; }

    public string ClassName { get; set; } = null!;

    public string JoinCode { get; set; } = null!;

    public int TeacherId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ClassroomMember> ClassroomMembers { get; set; } = new List<ClassroomMember>();

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public virtual User Teacher { get; set; } = null!;
}
