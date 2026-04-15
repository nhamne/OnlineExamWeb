using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class Submission
{
    public int Id { get; set; }

    public int ExamSessionId { get; set; }

    public int StudentId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public int? Status { get; set; }

    public double? Score { get; set; }

    public int? CorrectAnswersCount { get; set; }

    public int? WarningCount { get; set; }

    public virtual ExamSession ExamSession { get; set; } = null!;

    public virtual User Student { get; set; } = null!;

    public virtual ICollection<SubmissionDetail> SubmissionDetails { get; set; } = new List<SubmissionDetail>();
}
