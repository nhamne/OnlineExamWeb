using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OnlineExam.Services.Search;

public class MeiliSearchService : IMeiliSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly ConcurrentDictionary<string, bool> SettingsCache = new();

    private readonly HttpClient _httpClient;
    private readonly ILogger<MeiliSearchService> _logger;

    private readonly bool _enabled;
    private readonly string _classroomIndex;
    private readonly string _studentIndex;
    private readonly string _examPaperIndex;
    private readonly string _examSessionIndex;
    private readonly string _studentClassroomIndex;
    private readonly string _studentExamSessionIndex;
    private readonly string _studentSubmissionIndex;

    public bool IsEnabled => _enabled;

    // Xem meilisearch có đang chạy không
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return json.RootElement.TryGetProperty("status", out var status) &&
                   status.GetString()?.Equals("available", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MeiliSearch health check failed.");
            return false;
        }
    }

    public MeiliSearchService(HttpClient httpClient, IConfiguration configuration, ILogger<MeiliSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("MeiliSearch");
        _enabled = section.GetValue<bool?>("Enabled") ?? false;

        var hostUrl = section.GetValue<string>("HostUrl")?.TrimEnd('/');
        var apiKey = section.GetValue<string>("ApiKey");

        _classroomIndex = section.GetValue<string>("ClassroomsIndex") ?? "teacher_classrooms";
        _studentIndex = section.GetValue<string>("StudentsIndex") ?? "classroom_students";
        _examPaperIndex = section.GetValue<string>("ExamPapersIndex") ?? "teacher_exam_papers";
        _examSessionIndex = section.GetValue<string>("ExamSessionsIndex") ?? "teacher_exam_sessions";
        _studentClassroomIndex = section.GetValue<string>("StudentClassroomsIndex") ?? "student_classrooms";
        _studentExamSessionIndex = section.GetValue<string>("StudentExamSessionsIndex") ?? "student_exam_sessions";
        _studentSubmissionIndex = section.GetValue<string>("StudentSubmissionsIndex") ?? "student_submissions";

        if (!string.IsNullOrWhiteSpace(hostUrl))
        {
            _httpClient.BaseAddress = new Uri(hostUrl + "/");
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task IndexTeacherClassroomsAsync(int teacherId, IEnumerable<ClassroomSearchDocument> classrooms, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_classroomIndex, new[] { "teacherId" }, cancellationToken);

            var payload = classrooms.Select(x =>
            {
                x.TeacherId = teacherId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_classroomIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index classrooms into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchTeacherClassroomIdsAsync(int teacherId, string keyword, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_classroomIndex, new[] { "teacherId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"teacherId = {teacherId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_classroomIndex}/search", request, cancellationToken);
    }

    public async Task IndexClassStudentsAsync(int classroomId, IEnumerable<StudentSearchDocument> students, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_studentIndex, new[] { "classroomId" }, cancellationToken);

            var payload = students.Select(x =>
            {
                x.ClassroomId = classroomId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_studentIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index class students into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchClassStudentIdsAsync(int classroomId, string keyword, int limit = 500, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_studentIndex, new[] { "classroomId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"classroomId = {classroomId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_studentIndex}/search", request, cancellationToken);
    }

    public async Task IndexTeacherExamPapersAsync(int teacherId, IEnumerable<ExamPaperSearchDocument> examPapers, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_examPaperIndex, new[] { "teacherId" }, cancellationToken);

            var payload = examPapers.Select(x =>
            {
                x.TeacherId = teacherId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_examPaperIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index exam papers into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchTeacherExamPaperIdsAsync(int teacherId, string keyword, int limit = 300, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_examPaperIndex, new[] { "teacherId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"teacherId = {teacherId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_examPaperIndex}/search", request, cancellationToken);
    }

    public async Task IndexTeacherExamSessionsAsync(int teacherId, IEnumerable<ExamSessionSearchDocument> examSessions, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_examSessionIndex, new[] { "teacherId" }, cancellationToken);

            var payload = examSessions.Select(x =>
            {
                x.TeacherId = teacherId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_examSessionIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index exam sessions into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchTeacherExamSessionIdsAsync(int teacherId, string keyword, int limit = 400, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_examSessionIndex, new[] { "teacherId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"teacherId = {teacherId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_examSessionIndex}/search", request, cancellationToken);
    }

    public async Task IndexStudentClassroomsAsync(int studentId, IEnumerable<StudentClassroomSearchDocument> classrooms, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_studentClassroomIndex, new[] { "studentId" }, cancellationToken);

            var payload = classrooms.Select(x =>
            {
                x.StudentId = studentId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_studentClassroomIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index student classrooms into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchStudentClassroomIdsAsync(int studentId, string keyword, int limit = 300, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_studentClassroomIndex, new[] { "studentId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"studentId = {studentId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_studentClassroomIndex}/search", request, cancellationToken);
    }

    public async Task IndexStudentExamSessionsAsync(int studentId, IEnumerable<StudentExamSessionSearchDocument> examSessions, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_studentExamSessionIndex, new[] { "studentId" }, cancellationToken);

            var payload = examSessions.Select(x =>
            {
                x.StudentId = studentId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_studentExamSessionIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index student exam sessions into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchStudentExamSessionIdsAsync(int studentId, string keyword, int limit = 400, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_studentExamSessionIndex, new[] { "studentId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"studentId = {studentId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_studentExamSessionIndex}/search", request, cancellationToken);
    }

    public async Task IndexStudentSubmissionsAsync(int studentId, IEnumerable<StudentSubmissionSearchDocument> submissions, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        try
        {
            await EnsureFilterableAttributesAsync(_studentSubmissionIndex, new[] { "studentId" }, cancellationToken);

            var payload = submissions.Select(x =>
            {
                x.StudentId = studentId;
                return x;
            }).ToList();

            await PostJsonAsync($"indexes/{_studentSubmissionIndex}/documents?primaryKey=Id", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index student submissions into MeiliSearch. Falling back to SQL only.");
        }
    }

    public async Task<IReadOnlyList<int>> SearchStudentSubmissionIdsAsync(int studentId, string keyword, int limit = 500, CancellationToken cancellationToken = default)
    {
        if (!_enabled || _httpClient.BaseAddress is null || string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<int>();
        }

        await EnsureFilterableAttributesAsync(_studentSubmissionIndex, new[] { "studentId" }, cancellationToken);

        var request = new
        {
            q = keyword,
            limit,
            filter = $"studentId = {studentId}",
            attributesToRetrieve = new[] { "id" }
        };

        return await SearchIdListAsync($"indexes/{_studentSubmissionIndex}/search", request, cancellationToken);
    }

    private async Task EnsureFilterableAttributesAsync(string indexName, IEnumerable<string> attributes, CancellationToken cancellationToken)
    {
        if (!_enabled || _httpClient.BaseAddress is null)
        {
            return;
        }

        if (SettingsCache.ContainsKey(indexName))
        {
            return;
        }

        try
        {
            await PostJsonAsync($"indexes/{indexName}/settings/filterable-attributes", attributes, cancellationToken, HttpMethod.Put);
            SettingsCache[indexName] = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to configure filterable attributes for index {IndexName}", indexName);
        }
    }

    private async Task<IReadOnlyList<int>> SearchIdListAsync(string path, object requestPayload, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await PostJsonAsync(path, requestPayload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<int>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int>();
            }

            var ids = new List<int>();
            foreach (var hit in hits.EnumerateArray())
            {
                if (hit.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id))
                {
                    ids.Add(id);
                    continue;
                }

                if (hit.TryGetProperty("Id", out var idUpper) && idUpper.TryGetInt32(out var idValue))
                {
                    ids.Add(idValue);
                }
            }

            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search request failed for endpoint {Endpoint}", path);
            return Array.Empty<int>();
        }
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string path, object payload, CancellationToken cancellationToken, HttpMethod? method = null)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return await _httpClient.SendAsync(request, cancellationToken);
    }
}
