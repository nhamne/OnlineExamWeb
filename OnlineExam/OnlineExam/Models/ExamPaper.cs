using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class ExamPaper
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public int TeacherId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual User Teacher { get; set; } = null!;
}
