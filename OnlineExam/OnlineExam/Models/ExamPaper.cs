using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineExam.Models;

public partial class ExamPaper
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public int DurationInMinutes { get; set; }

    public string? Subject { get; set; }

    public string? Status { get; set; }

    [NotMapped]
    public int? Duration
    {
        get => DurationInMinutes;
        set => DurationInMinutes = value ?? DurationInMinutes;
    }

    public int TeacherId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }

<<<<<<< HEAD
    public string? Status { get; set; }

    [NotMapped]
    public int? Duration { get; set; }

=======
>>>>>>> origin/dev-nham
    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual User Teacher { get; set; } = null!;
}
