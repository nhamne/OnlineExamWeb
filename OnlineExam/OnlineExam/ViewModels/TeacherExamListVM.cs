using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels;

public class TeacherExamListVM
{
    public List<TeacherExamItemVM> Exams { get; set; } = new();

    public string? SearchKeyword { get; set; }

    public string SortBy { get; set; } = "created_desc";
}

public class TeacherExamItemVM
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }

    public int QuestionCount { get; set; }

    public int SessionCount { get; set; }
}
