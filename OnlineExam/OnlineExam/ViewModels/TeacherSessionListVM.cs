using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels;

public class TeacherSessionListVM
{
    public List<TeacherSessionItemVM> Sessions { get; set; } = new();

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 5;

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public List<SessionOptionVM> Classrooms { get; set; } = new();

    public List<SessionOptionVM> ExamPapers { get; set; } = new();

    public string? SearchKeyword { get; set; }

    public string StatusFilter { get; set; } = "all";

    public string SortBy { get; set; } = "start_desc";
}

public class SessionOptionVM
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? DurationInMinutes { get; set; }
}

public class TeacherSessionItemVM
{
    public int Id { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string ExamTitle { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int DurationInMinutes { get; set; }

    public int SubmissionCount { get; set; }

    public string Status { get; set; } = string.Empty;
}
