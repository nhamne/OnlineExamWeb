using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class Question
{
    public int Id { get; set; }

    public int ExamPaperId { get; set; }

    public string Content { get; set; } = null!;

    public string OptionA { get; set; } = null!;

    public string OptionB { get; set; } = null!;

    public string OptionC { get; set; } = null!;

    public string OptionD { get; set; } = null!;

    public string CorrectOption { get; set; } = null!;

    public string? Explanation { get; set; }

    public virtual ExamPaper ExamPaper { get; set; } = null!;

    public virtual ICollection<SubmissionDetail> SubmissionDetails { get; set; } = new List<SubmissionDetail>();
}
