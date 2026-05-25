using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels;

public class SessionMonitorPageVM
{
    public int SessionId { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string ExamTitle { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int DurationInMinutes { get; set; }

    public string? SearchKeyword { get; set; }

    public string StatusFilter { get; set; } = "all";

    public int TotalStudents { get; set; }

    public int SubmittedCount { get; set; }

    public int InProgressCount { get; set; }

    public int NotStartedCount { get; set; }

    public int LateNotSubmittedCount { get; set; }

    public int ViolationCount { get; set; }

    public List<SessionMonitorStudentItemVM> Students { get; set; } = new();
}

public class SessionMonitorStudentItemVM
{
    public int StudentId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int? SubmissionId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public double? Score { get; set; }

    public int? CorrectAnswersCount { get; set; }

    public int WarningCount { get; set; }

    public int? TimeSpentMinutes { get; set; }

    public string SubmissionStatus { get; set; } = string.Empty;
}
