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
