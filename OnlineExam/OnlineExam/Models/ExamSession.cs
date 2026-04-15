using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class ExamSession
{
    public int Id { get; set; }

    public string SessionName { get; set; } = null!;

    public int ClassroomId { get; set; }

    public int ExamPaperId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int DurationInMinutes { get; set; }

    public string? SessionPassword { get; set; }

    public bool? AllowViewScore { get; set; }

    public bool? IsShuffled { get; set; }

    public virtual Classroom Classroom { get; set; } = null!;

    public virtual ExamPaper ExamPaper { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
