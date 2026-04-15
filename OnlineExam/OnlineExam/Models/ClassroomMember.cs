using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class ClassroomMember
{
    public int Id { get; set; }

    public int ClassroomId { get; set; }

    public int StudentId { get; set; }

    public DateTime? JoinedAt { get; set; }

    public virtual Classroom Classroom { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
