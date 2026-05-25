using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels;

public class ClassroomManageVM
{
    public int ClassroomId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string JoinCode { get; set; } = string.Empty;

    public string? SearchKeyword { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public List<ClassroomStudentItemVM> Students { get; set; } = new();
}

public class ClassroomStudentItemVM
{
    public int StudentId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime? JoinedAt { get; set; }
}
