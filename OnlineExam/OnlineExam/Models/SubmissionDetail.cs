using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class SubmissionDetail
{
    public int Id { get; set; }

    public int SubmissionId { get; set; }

    public int QuestionId { get; set; }

    public string? SelectedOption { get; set; }

    public bool? IsCorrect { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual Submission Submission { get; set; } = null!;
}
