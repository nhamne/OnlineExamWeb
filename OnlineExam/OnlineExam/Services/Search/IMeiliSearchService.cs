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

    Task IndexStudentClassroomsAsync(int studentId, IEnumerable<StudentClassroomSearchDocument> classrooms, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchStudentClassroomIdsAsync(int studentId, string keyword, int limit = 300, CancellationToken cancellationToken = default);

    Task IndexStudentExamSessionsAsync(int studentId, IEnumerable<StudentExamSessionSearchDocument> examSessions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchStudentExamSessionIdsAsync(int studentId, string keyword, int limit = 400, CancellationToken cancellationToken = default);

    Task IndexStudentSubmissionsAsync(int studentId, IEnumerable<StudentSubmissionSearchDocument> submissions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> SearchStudentSubmissionIdsAsync(int studentId, string keyword, int limit = 500, CancellationToken cancellationToken = default);
}
