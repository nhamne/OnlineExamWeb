using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OnlineExam.Services.Search;

public interface IMeiliSearchService
{
    bool IsEnabled { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task IndexTeacherClassroomsAsync(int teacherId, IEnumerable<ClassroomSearchDocument> classrooms, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchTeacherClassroomIdsAsync(int teacherId, string keyword, int limit = 200, CancellationToken cancellationToken = default);

    Task IndexClassStudentsAsync(int classroomId, IEnumerable<StudentSearchDocument> students, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchClassStudentIdsAsync(int classroomId, string keyword, int limit = 500, CancellationToken cancellationToken = default);

    Task IndexTeacherExamPapersAsync(int teacherId, IEnumerable<ExamPaperSearchDocument> examPapers, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchTeacherExamPaperIdsAsync(int teacherId, string keyword, int limit = 300, CancellationToken cancellationToken = default);

    Task IndexTeacherExamSessionsAsync(int teacherId, IEnumerable<ExamSessionSearchDocument> examSessions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchTeacherExamSessionIdsAsync(int teacherId, string keyword, int limit = 400, CancellationToken cancellationToken = default);
}
