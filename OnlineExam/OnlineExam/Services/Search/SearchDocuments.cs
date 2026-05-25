using System;

// quy định mỗi loại dữ liệu được đưa vào MeiliSearch dưới dạng nào:
namespace OnlineExam.Services.Search;

public sealed class ClassroomSearchDocument
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string JoinCode { get; set; } = string.Empty;
}

public sealed class StudentSearchDocument
{
    public int Id { get; set; }

    public int ClassroomId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public sealed class ExamPaperSearchDocument
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string Title { get; set; } = string.Empty;
}

public sealed class ExamSessionSearchDocument
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string ExamTitle { get; set; } = string.Empty;
}

public sealed class StudentClassroomSearchDocument
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;

    public string JoinCode { get; set; } = string.Empty;
}

public sealed class StudentExamSessionSearchDocument
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string SessionName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;
}

public sealed class StudentSubmissionSearchDocument
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public double? Score { get; set; }

    public DateTime? SubmittedAt { get; set; }
}
