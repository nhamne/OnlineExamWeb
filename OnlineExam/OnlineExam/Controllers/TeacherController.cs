using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OnlineExam.Models;
using OnlineExam.Services.Search;
using OnlineExam.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace OnlineExam.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly OnlineExamDbContext _context;
    private readonly IMeiliSearchService? _meiliSearch;

    public TeacherController(OnlineExamDbContext context, IMeiliSearchService? meiliSearch = null)
    {
        _context = context;
        _meiliSearch = meiliSearch;
    }

    public async Task<IActionResult> Index()
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue)
        {
            return Unauthorized();
        }

        var now = DateTime.Now;
        var teacher = await _context.Users.AsNoTracking()
            .Where(u => u.Id == teacherId.Value)
            .Select(u => new { u.FullName })
            .FirstOrDefaultAsync();

        if (teacher is null)
        {
            return Unauthorized();
        }

        var dashboardData = new TeacherDashboardVM
        {
            TeacherName = string.IsNullOrWhiteSpace(teacher.FullName) ? "Giảng viên" : teacher.FullName,
            TotalClasses = await _context.Classrooms.AsNoTracking().CountAsync(c => c.TeacherId == teacherId.Value && c.IsDeleted == false),
            TotalExams = await _context.ExamPapers.AsNoTracking().CountAsync(e => e.TeacherId == teacherId.Value && e.IsDeleted == false),
            OngoingSessions = await _context.ExamSessions.AsNoTracking().CountAsync(s => s.ExamPaper.TeacherId == teacherId.Value && s.StartTime <= now && s.EndTime >= now),
            UpcomingSessions = await _context.ExamSessions.AsNoTracking().CountAsync(s => s.ExamPaper.TeacherId == teacherId.Value && s.StartTime > now),
            RecentSessions = await _context.ExamSessions.AsNoTracking()
                .Where(s => s.ExamPaper.TeacherId == teacherId.Value)
                .OrderByDescending(s => s.StartTime)
                .Take(3)
                .Select(s => new SessionItemVM
                {
                    SessionName = s.SessionName,
                    ClassName = s.Classroom.ClassName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                })
                .ToListAsync(),
            SessionsToday = await _context.ExamSessions.AsNoTracking().CountAsync(s => s.ExamPaper.TeacherId == teacherId.Value && s.StartTime.Date == now.Date)
        };

        return View(dashboardData);
    }

    [HttpGet]
    public async Task<IActionResult> Reports()
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var teacherClassIds = await _context.Classrooms
            .Where(c => c.TeacherId == teacherId.Value && c.IsDeleted == false)
            .Select(c => c.Id)
            .ToListAsync();

        var totalStudents = await _context.ClassroomMembers
            .Where(cm => teacherClassIds.Contains(cm.ClassroomId))
            .Select(cm => cm.StudentId)
            .Distinct()
            .CountAsync();

        var teacherSubmissions = await _context.Submissions
            .Include(s => s.ExamSession)
            .ThenInclude(es => es.Classroom)
            .Where(s => s.ExamSession.ExamPaper.TeacherId == teacherId.Value && (s.Status == 1 || s.Status == 2))
            .ToListAsync();

        var totalSubmissions = teacherSubmissions.Count;
        var averageScore = totalSubmissions > 0 ? teacherSubmissions.Average(s => s.Score ?? 0) : 0;

        var score0To4 = teacherSubmissions.Count(s => (s.Score ?? 0) < 4);
        var score4To6 = teacherSubmissions.Count(s => (s.Score ?? 0) >= 4 && (s.Score ?? 0) < 6);
        var score6To8 = teacherSubmissions.Count(s => (s.Score ?? 0) >= 6 && (s.Score ?? 0) < 8);
        var score8To10 = teacherSubmissions.Count(s => (s.Score ?? 0) >= 8);

        var recentPerformances = teacherSubmissions
            .GroupBy(s => new { s.ExamSessionId, s.ExamSession.SessionName, s.ExamSession.Classroom.ClassName })
            .Select(g => new SessionPerformanceVM
            {
                SessionId = g.Key.ExamSessionId,
                SessionName = g.Key.SessionName,
                ClassName = g.Key.ClassName,
                TotalSubmissions = g.Count(),
                AverageScore = Math.Round(g.Average(s => s.Score ?? 0), 2)
            })
            .OrderByDescending(sp => sp.SessionId)
            .Take(5)
            .ToList();

        var vm = new TeacherReportVM
        {
            TotalStudents = totalStudents,
            TotalSubmissions = totalSubmissions,
            AverageScore = Math.Round(averageScore, 2),
            Score0To4 = score0To4,
            Score4To6 = score4To6,
            Score6To8 = score6To8,
            Score8To10 = score8To10,
            RecentPerformances = recentPerformances
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Classes(string? searchKeyword, string studentFilter = "all", string sortBy = "created_desc", int page = 1)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var baseQuery = _context.Classrooms.AsNoTracking().Where(c => c.TeacherId == teacherId.Value && c.IsDeleted == false);
        var showMeiliWarning = false;

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim();
            var meiliAvailable = await IsMeiliAvailableAsync();
            showMeiliWarning = !meiliAvailable;

            if (meiliAvailable)
            {
                var searchableClasses = await baseQuery.Select(c => new ClassroomSearchDocument
                {
                    Id = c.Id,
                    TeacherId = c.TeacherId,
                    ClassName = c.ClassName,
                    JoinCode = c.JoinCode
                }).ToListAsync();

                await _meiliSearch!.IndexTeacherClassroomsAsync(teacherId.Value, searchableClasses);
                var matchedIds = await _meiliSearch.SearchTeacherClassroomIdsAsync(teacherId.Value, keyword);
                if (matchedIds.Count > 0)
                {
                    baseQuery = baseQuery.Where(c => matchedIds.Contains(c.Id));
                }
                else
                {
                    baseQuery = baseQuery.Where(c => c.ClassName.Contains(keyword) || c.JoinCode.Contains(keyword));
                }
            }
            else
            {
                baseQuery = baseQuery.Where(c => c.ClassName.Contains(keyword) || c.JoinCode.Contains(keyword));
            }
        }

        var projectedQuery = baseQuery.Select(c => new ClassroomVM
        {
            Id = c.Id,
            ClassName = c.ClassName,
            JoinCode = c.JoinCode,
            StudentCount = c.ClassroomMembers.Count,
            CreatedAt = c.CreatedAt,
            IsActive = c.IsDeleted == false
        });

        projectedQuery = studentFilter switch
        {
            "empty" => projectedQuery.Where(c => c.StudentCount == 0),
            "small" => projectedQuery.Where(c => c.StudentCount > 0 && c.StudentCount <= 30),
            "large" => projectedQuery.Where(c => c.StudentCount > 30),
            _ => projectedQuery
        };

        projectedQuery = sortBy switch
        {
            "created_asc" => projectedQuery.OrderBy(c => c.CreatedAt),
            "name_asc" => projectedQuery.OrderBy(c => c.ClassName),
            "name_desc" => projectedQuery.OrderByDescending(c => c.ClassName),
            "students_desc" => projectedQuery.OrderByDescending(c => c.StudentCount),
            "students_asc" => projectedQuery.OrderBy(c => c.StudentCount),
            _ => projectedQuery.OrderByDescending(c => c.CreatedAt)
        };

        const int pageSize = 6;
        page = Math.Max(1, page);

        var totalItems = await projectedQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);

        var classList = await projectedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return View(new ClassroomListPageVM
        {
            Classes = classList,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            SearchKeyword = searchKeyword,
            StudentFilter = studentFilter,
            SortBy = sortBy,
            ShowMeiliSearchWarning = showMeiliWarning
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateClass(string className)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(className)) return Json(new { success = false, message = "Tên lớp học không được để trống!" });

        try
        {
            var joinCode = GenerateJoinCode();
            while (await _context.Classrooms.AnyAsync(c => c.JoinCode == joinCode))
            {
                joinCode = GenerateJoinCode();
            }

            var classroom = new Classroom
            {
                ClassName = className.Trim(),
                JoinCode = joinCode,
                TeacherId = teacherId.Value,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            _context.Classrooms.Add(classroom);
            await _context.SaveChangesAsync();

            if (await IsMeiliAvailableAsync())
            {
                await _meiliSearch!.IndexTeacherClassroomsAsync(teacherId.Value, new[]
                {
                    new ClassroomSearchDocument { Id = classroom.Id, TeacherId = teacherId.Value, ClassName = classroom.ClassName, JoinCode = classroom.JoinCode }
                });
            }

            return Json(new { success = true, message = "Tạo lớp học thành công!", joinCode, classroomId = classroom.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lỗi: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateClass(int classId, string className)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();
        if (classId <= 0) return Json(new { success = false, message = "Lớp học không hợp lệ." });
        if (string.IsNullOrWhiteSpace(className)) return Json(new { success = false, message = "Tên lớp học không được để trống." });

        var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId.Value && c.IsDeleted == false);
        if (classroom is null) return Json(new { success = false, message = "Không tìm thấy lớp học để cập nhật." });

        classroom.ClassName = className.Trim();
        await _context.SaveChangesAsync();

        if (await IsMeiliAvailableAsync())
        {
            await _meiliSearch!.IndexTeacherClassroomsAsync(teacherId.Value, new[]
            {
                new ClassroomSearchDocument { Id = classroom.Id, TeacherId = teacherId.Value, ClassName = classroom.ClassName, JoinCode = classroom.JoinCode }
            });
        }

        return Json(new { success = true, message = "Cập nhật lớp học thành công.", classroomId = classroom.Id, className = classroom.ClassName });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClass(int classId)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();
        if (classId <= 0) return Json(new { success = false, message = "Lớp học không hợp lệ." });

        var classroom = await _context.Classrooms.FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId.Value && c.IsDeleted == false);
        if (classroom is null) return Json(new { success = false, message = "Không tìm thấy lớp học để xóa." });

        classroom.IsDeleted = true;
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Đã xóa lớp học thành công." });
    }

    [HttpGet]
    public async Task<IActionResult> ManageClass(int id, string? searchKeyword, int page = 1, int pageSize = 10)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 5, 50);

        var classroom = await _context.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId.Value && c.IsDeleted == false);
        if (classroom is null) return NotFound();

        var studentQuery = _context.ClassroomMembers.AsNoTracking()
            .Where(cm => cm.ClassroomId == id)
            .Select(cm => new ClassroomStudentItemVM
            {
                StudentId = cm.Student.Id,
                FullName = cm.Student.FullName,
                Email = cm.Student.Email,
                JoinedAt = cm.JoinedAt
            });

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim();
            var meiliAvailable = await IsMeiliAvailableAsync();
            if (meiliAvailable)
            {
                var searchableStudents = await _context.ClassroomMembers.AsNoTracking()
                    .Where(cm => cm.ClassroomId == id)
                    .Select(cm => new StudentSearchDocument { Id = cm.Student.Id, ClassroomId = id, FullName = cm.Student.FullName, Email = cm.Student.Email })
                    .ToListAsync();

                await _meiliSearch!.IndexClassStudentsAsync(id, searchableStudents);
                var matchedStudentIds = await _meiliSearch.SearchClassStudentIdsAsync(id, keyword);
                if (matchedStudentIds.Count > 0)
                {
                    studentQuery = studentQuery.Where(s => matchedStudentIds.Contains(s.StudentId));
                }
                else
                {
                    studentQuery = studentQuery.Where(s => s.FullName.Contains(keyword) || s.Email.Contains(keyword));
                }
            }
            else
            {
                studentQuery = studentQuery.Where(s => s.FullName.Contains(keyword) || s.Email.Contains(keyword));
            }
        }

        var totalItems = await studentQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(Math.Max(1, page), totalPages);

        var students = await studentQuery.OrderBy(s => s.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return View(new ClassroomManageVM
        {
            ClassroomId = classroom.Id,
            ClassName = classroom.ClassName,
            JoinCode = classroom.JoinCode,
            SearchKeyword = searchKeyword,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Students = students
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudentFromClass(int id, int studentId, string? searchKeyword, int page = 1, int pageSize = 10)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        if (!await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == id && c.TeacherId == teacherId.Value && c.IsDeleted == false))
        {
            return NotFound();
        }

        var member = await _context.ClassroomMembers.FirstOrDefaultAsync(cm => cm.ClassroomId == id && cm.StudentId == studentId);
        if (member is null)
        {
            TempData["ToastMessage"] = "Không tìm thấy học sinh trong lớp.";
            TempData["ToastType"] = "error";
            return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
        }

        _context.ClassroomMembers.Remove(member);
        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = "Đã xóa học sinh khỏi lớp.";
        TempData["ToastType"] = "success";
        return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudentsFromClass(int id, List<int>? studentIds, string? searchKeyword, int page = 1, int pageSize = 10)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        if (!await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == id && c.TeacherId == teacherId.Value && c.IsDeleted == false))
        {
            return NotFound();
        }

        var selectedIds = studentIds?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();
        if (selectedIds.Count == 0)
        {
            TempData["ToastMessage"] = "Bạn chưa chọn học sinh để xóa.";
            TempData["ToastType"] = "error";
            return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
        }

        var members = await _context.ClassroomMembers.Where(cm => cm.ClassroomId == id && selectedIds.Contains(cm.StudentId)).ToListAsync();
        if (members.Count == 0)
        {
            TempData["ToastMessage"] = "Không tìm thấy học sinh phù hợp để xóa.";
            TempData["ToastType"] = "error";
            return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
        }

        _context.ClassroomMembers.RemoveRange(members);
        await _context.SaveChangesAsync();

        TempData["ToastMessage"] = $"Đã xóa {members.Count} học sinh khỏi lớp.";
        TempData["ToastType"] = "success";
        return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteStudents([FromBody] DeleteStudentsRequest req)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();
        if (req == null || req.Id <= 0) return BadRequest();

        if (!await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == req.Id && c.TeacherId == teacherId.Value && c.IsDeleted == false))
        {
            return NotFound();
        }

        var selectedIds = req.StudentIds?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();
        if (selectedIds.Count == 0) return BadRequest();

        var members = await _context.ClassroomMembers.Where(cm => cm.ClassroomId == req.Id && selectedIds.Contains(cm.StudentId)).ToListAsync();
        if (members.Count == 0) return NotFound();

        _context.ClassroomMembers.RemoveRange(members);
        await _context.SaveChangesAsync();

        return Json(new { success = true, count = members.Count });
    }

    public class DeleteStudentsRequest
    {
        public int Id { get; set; }
        public List<int>? StudentIds { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> ExportClassStudents(int id, string? studentIds, string format = "csv")
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        if (!await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == id && c.TeacherId == teacherId.Value && c.IsDeleted == false))
        {
            return NotFound();
        }

        var ids = new List<int>();
        if (!string.IsNullOrWhiteSpace(studentIds))
        {
            ids = studentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => { int.TryParse(s, out var v); return v; }).Where(v => v > 0).ToList();
        }

        if (ids.Count == 0)
        {
            return BadRequest("No students selected");
        }

        var students = await _context.ClassroomMembers.AsNoTracking()
            .Where(cm => cm.ClassroomId == id && ids.Contains(cm.StudentId))
            .Select(cm => new { cm.Student.FullName, cm.Student.Email, JoinedAt = cm.JoinedAt })
            .ToListAsync();

        var csvLines = new List<string> { "FullName,Email,JoinedAt" };
        foreach (var s in students)
        {
            var joined = s.JoinedAt.HasValue ? s.JoinedAt.Value.ToString("dd/MM/yyyy HH:mm") : "";
            var line = $"\"{s.FullName.Replace("\"", "\"\"")}\",\"{s.Email}\",\"{joined}\"";
            csvLines.Add(line);
        }

        var csv = string.Join("\r\n", csvLines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileName = $"students_{id}_{DateTime.Now:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportSessionSubmissions(int id, string? studentIds, string format = "csv")
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var session = await _context.ExamSessions.AsNoTracking()
            .Include(s => s.Classroom)
            .FirstOrDefaultAsync(s => s.Id == id && s.Classroom.TeacherId == teacherId.Value && s.Classroom.IsDeleted == false);

        if (session == null)
        {
            return NotFound();
        }

        var ids = new List<int>();
        if (!string.IsNullOrWhiteSpace(studentIds))
        {
            ids = studentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => { int.TryParse(s, out var v); return v; }).Where(v => v > 0).ToList();
        }

        if (ids.Count == 0)
        {
            return BadRequest("No students selected");
        }

        var now = DateTime.Now;

        var studentRows = await _context.ClassroomMembers.AsNoTracking()
            .Where(cm => cm.ClassroomId == session.ClassroomId && ids.Contains(cm.StudentId))
            .Select(cm => new
            {
                cm.StudentId,
                cm.Student.FullName,
                cm.Student.Email,
                Submission = _context.Submissions.AsNoTracking()
                    .Where(s => s.ExamSessionId == id && s.StudentId == cm.StudentId)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefault()
            })
            .OrderBy(x => x.FullName)
            .ToListAsync();

        var csvLines = new List<string> { "FullName,Email,Score,CorrectAnswers,TimeSpentMinutes,WarningCount,StartedAt,SubmittedAt,Status" };
        foreach (var x in studentRows)
        {
            var submission = x.Submission;
            var statusText = MapSubmissionStatus(submission?.Status, submission?.SubmittedAt, session.EndTime, now);
            var timeSpentMinutes = CalculateTimeSpentMinutes(submission?.StartedAt, submission?.SubmittedAt, session.EndTime, now);

            var score = submission?.Score?.ToString("0.##") ?? "";
            var correctCount = submission?.CorrectAnswersCount?.ToString() ?? "";
            var timeSpent = timeSpentMinutes?.ToString() ?? "";
            var warningCount = submission?.WarningCount.ToString() ?? "0";
            var startedAt = submission != null ? submission.StartedAt.ToString("dd/MM/yyyy HH:mm") : "";
            var submittedAt = submission != null && submission.SubmittedAt.HasValue ? submission.SubmittedAt.Value.ToString("dd/MM/yyyy HH:mm") : "";

            var line = $"\"{x.FullName.Replace("\"", "\"\"")}\",\"{x.Email}\",\"{score}\",\"{correctCount}\",\"{timeSpent}\",\"{warningCount}\",\"{startedAt}\",\"{submittedAt}\",\"{statusText}\"";
            csvLines.Add(line);
        }

        var csv = string.Join("\r\n", csvLines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileName = $"session_{id}_monitor_{DateTime.Now:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExamsSummary(string? searchKeyword, string sortBy = "created_desc")
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var showMeiliWarning = false;
        var baseQuery = _context.ExamPapers.AsNoTracking().Where(e => e.TeacherId == teacherId.Value && e.IsDeleted == false);

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim();
            var meiliAvailable = await IsMeiliAvailableAsync();
            showMeiliWarning = !meiliAvailable;

            if (meiliAvailable)
            {
                var allExams = await _context.ExamPapers.AsNoTracking().Where(e => e.TeacherId == teacherId.Value && e.IsDeleted == false).ToListAsync();
                var searchableExams = allExams.Select(e => new ExamPaperSearchDocument
                {
                    Id = e.Id,
                    TeacherId = e.TeacherId,
                    Title = e.Title
                });
                await _meiliSearch!.IndexTeacherExamPapersAsync(teacherId.Value, searchableExams);
                var matchedIds = await _meiliSearch.SearchTeacherExamPaperIdsAsync(teacherId.Value, keyword);
                
                baseQuery = baseQuery.Where(e => matchedIds.Contains(e.Id));
            }
            else
            {
                baseQuery = baseQuery.Where(e => e.Title.Contains(keyword) || (e.Subject != null && e.Subject.Contains(keyword)));
            }
        }

        var projectedQuery = baseQuery.Select(e => new TeacherExamItemVM
        {
            Id = e.Id,
            Title = e.Title,
            CreatedAt = e.CreatedAt,
            QuestionCount = e.Questions.Count,
            SessionCount = e.ExamSessions.Count
        });

        projectedQuery = sortBy switch
        {
            "created_asc" => projectedQuery.OrderBy(e => e.CreatedAt),
            "name_asc" => projectedQuery.OrderBy(e => e.Title),
            "name_desc" => projectedQuery.OrderByDescending(e => e.Title),
            "questions_desc" => projectedQuery.OrderByDescending(e => e.QuestionCount),
            "sessions_desc" => projectedQuery.OrderByDescending(e => e.SessionCount),
            _ => projectedQuery.OrderByDescending(e => e.CreatedAt)
        };

        return View(new TeacherExamListVM
        {
            Exams = await projectedQuery.ToListAsync(),
            SearchKeyword = searchKeyword,
            SortBy = sortBy,
            ShowMeiliSearchWarning = showMeiliWarning
        });
    }

    [HttpGet]
    public async Task<IActionResult> Exams(string search = null, string subject = null, string title = null, string status = null, int page = 1)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        const int pageSize = 10;
        var showMeiliWarning = false;
        
        var baseQuery = _context.ExamPapers
            .Where(e => e.TeacherId == teacherId.Value && e.IsDeleted != true)
            .Include(e => e.Teacher)
            .Include(e => e.Questions)
            .Include(e => e.ExamSessions)
                .ThenInclude(es => es.Classroom)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            var meiliAvailable = await IsMeiliAvailableAsync();
            showMeiliWarning = !meiliAvailable;

            if (meiliAvailable)
            {
                var allExams = await _context.ExamPapers.AsNoTracking().Where(e => e.TeacherId == teacherId.Value && e.IsDeleted != true).ToListAsync();
                var searchableExams = allExams.Select(e => new ExamPaperSearchDocument
                {
                    Id = e.Id,
                    TeacherId = e.TeacherId,
                    Title = e.Title
                });
                await _meiliSearch!.IndexTeacherExamPapersAsync(teacherId.Value, searchableExams);
                var matchedIds = await _meiliSearch.SearchTeacherExamPaperIdsAsync(teacherId.Value, keyword);
                
                baseQuery = baseQuery.Where(e => matchedIds.Contains(e.Id));
            }
            else
            {
                baseQuery = baseQuery.Where(e => e.Title.Contains(keyword) || (e.Subject != null && e.Subject.Contains(keyword)));
            }
        }
        if (!string.IsNullOrEmpty(subject)) baseQuery = baseQuery.Where(e => e.Subject == subject);
        if (!string.IsNullOrEmpty(title)) baseQuery = baseQuery.Where(e => e.Title == title);
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "Bản nháp")
            {
                baseQuery = baseQuery.Where(e => e.Questions.Count == 0);
            }
            else if (status == "Xuất bản")
            {
                baseQuery = baseQuery.Where(e => e.Questions.Count > 0);
            }
        }

        var totalExams = await baseQuery.CountAsync();
        ViewBag.TotalExams = totalExams;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalExams / (double)pageSize);
        ViewBag.CurrentSearch = search;
        ViewBag.CurrentSubject = subject;
        ViewBag.CurrentTitle = title;
        ViewBag.CurrentStatus = status;
        ViewBag.ShowMeiliSearchWarning = showMeiliWarning;
        ViewBag.PopularSubject = await _context.ExamPapers.Where(e => e.IsDeleted != true && !string.IsNullOrEmpty(e.Subject)).GroupBy(e => e.Subject).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefaultAsync() ?? "Chưa có môn nào";
        ViewBag.TotalQuestions = await _context.Questions.Where(q => q.ExamPaper.IsDeleted != true).CountAsync();
        ViewBag.AllSubjects = await _context.ExamPapers.Where(e => e.IsDeleted != true && !string.IsNullOrEmpty(e.Subject)).Select(e => e.Subject).Distinct().ToListAsync();
        ViewBag.AllExamTitles = await _context.ExamPapers.Where(e => e.IsDeleted != true).Select(e => e.Title).Distinct().ToListAsync();

        var exams = await baseQuery.OrderByDescending(e => e.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        foreach (var exam in exams)
        {
            exam.Duration = exam.DurationInMinutes;
        }

        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExam(int id, string format)
    {
        var exam = await _context.ExamPapers.Include(e => e.Questions).Include(e => e.Teacher).FirstOrDefaultAsync(e => e.Id == id);
        if (exam == null) return NotFound(new { success = false, message = "Không tìm thấy đề thi" });

        exam.Duration = exam.DurationInMinutes;
        ViewBag.Format = format;
        if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.Append("Content-Disposition", $"attachment; filename=DeThi_{id}.doc");
            return View("ExportDocument", exam);
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return View("ExportDocument", exam);
        }

        return BadRequest("Không hỗ trợ định dạng này.");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteExams([FromBody] List<int> ids)
    {
        if (ids == null || !ids.Any()) return Json(new { success = false, message = "Vui lòng chọn ít nhất một đề thi để xóa!" });

        try
        {
            var deleteExams = await _context.ExamPapers.Where(e => ids.Contains(e.Id)).ToListAsync();
            foreach (var exam in deleteExams) exam.IsDeleted = true;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CopyExams([FromBody] List<int> ids)
    {
        if (ids == null || !ids.Any()) return Json(new { success = false, message = "Vui lòng chọn ít nhất một đề thi để sao chép!" });

        try
        {
            var examsToCopy = await _context.ExamPapers.Include(e => e.Questions).Where(e => ids.Contains(e.Id)).ToListAsync();
            foreach (var oldExam in examsToCopy)
            {
                var newExam = new ExamPaper
                {
                    Title = oldExam.Title + " (Bản sao)",
                    Subject = oldExam.Subject,
                    TeacherId = oldExam.TeacherId,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false,
                    DurationInMinutes = oldExam.DurationInMinutes
                };

                foreach (var q in oldExam.Questions)
                {
                    newExam.Questions.Add(new Question
                    {
                        Content = q.Content,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectOption = q.CorrectOption,
                        Explanation = q.Explanation
                    });
                }

                _context.ExamPapers.Add(newExam);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
        }
    }

    public IActionResult CreateExam() => View();

    public IActionResult OcrScanner() => View();

    [HttpGet]
    public async Task<IActionResult> EditExam(int id)
    {
        var exam = await _context.ExamPapers.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id);
        if (exam == null) return NotFound("Không tìm thấy đề thi.");

        exam.Duration = exam.DurationInMinutes;
        return View(exam);
    }

    [HttpGet]
    public async Task<IActionResult> ExamDetails(int id)
    {
        var exam = await _context.ExamPapers.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id);
        if (exam == null) return NotFound("Không tìm thấy đề thi.");

        exam.Duration = exam.DurationInMinutes;
        return View(exam);
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeOCR(IFormFile file, [FromServices] IConfiguration config)
    {
        if (file == null || file.Length == 0) return Json(new { success = false, message = "Vui lòng chọn file" });

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var base64String = Convert.ToBase64String(ms.ToArray());

            var apiKey = config["GeminiApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return Json(new { success = false, message = "Chưa cấu hình GeminiApiKey trong appsettings.json. Vui lòng thêm key của bạn vào." });

            var mimeType = file.ContentType;
            if (mimeType != "application/pdf" && !mimeType.StartsWith("image/")) return Json(new { success = false, message = "Gemini API chỉ hỗ trợ file PDF và Ảnh (JPG, PNG, WEBP)" });

            using var client = new HttpClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Bạn là trợ lý trích xuất câu hỏi trắc nghiệm. Nhiệm vụ của bạn là lấy TOÀN BỘ câu hỏi trắc nghiệm trong tài liệu này (bỏ qua những phần không phải là câu hỏi trắc nghiệm) và định dạng chúng thành một mảng JSON HỢP LỆ. Mỗi câu hỏi trong mảng JSON phải đúng cấu trúc object sau: {\"question\": \"nội dung câu hỏi\", \"options\": [\"đáp án A\", \"đáp án B\", \"đáp án C\", \"đáp án D\"], \"correct\": \"A\" (lấy ký tự đúng nếu biết, nếu không thể đoán thì để chuỗi rỗng), \"difficulty\": \"Dễ\"}. KHÔNG được trả thêm markdown (ví dụ như ```json). TRẢ VỀ DUY NHẤT MẢNG JSON. Đảm bảo parse đầy đủ dấu, tiếng Việt chuẩn." },
                            new { inline_data = new { mime_type = mimeType, data = base64String } }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(url, payload);
            var rawResult = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return Json(new { success = false, message = "Lỗi gọi AI: " + rawResult });

            using var doc = JsonDocument.Parse(rawResult);
            var jsonText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "[]";
            jsonText = jsonText.Replace("```json", "").Replace("```", "").Trim();
            var questionsList = JsonSerializer.Deserialize<List<object>>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Json(new { success = true, questions = questionsList });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lỗi xử lý file với AI: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SaveExam([FromBody] SaveExamRequest request)
    {
        try
        {
            var teacherId = GetTeacherId() ?? 1;
            var paper = new ExamPaper
            {
                Title = request.Title,
                Subject = request.Subject,
                DurationInMinutes = request.Duration ?? 45,
                TeacherId = teacherId,
                CreatedAt = DateTime.Now,
                IsDeleted = false,
                Status = request.Status ?? "Bản nháp"
            };

            foreach (var q in request.Questions)
            {
                paper.Questions.Add(new Question
                {
                    Content = q.Text,
                    OptionA = q.Options.FirstOrDefault(o => o.Label == "A")?.Text ?? "A",
                    OptionB = q.Options.FirstOrDefault(o => o.Label == "B")?.Text ?? "B",
                    OptionC = q.Options.FirstOrDefault(o => o.Label == "C")?.Text ?? "C",
                    OptionD = q.Options.FirstOrDefault(o => o.Label == "D")?.Text ?? "D",
                    CorrectOption = q.Options.FirstOrDefault(o => o.IsCorrect)?.Label ?? "A"
                });
            }

            _context.ExamPapers.Add(paper);
            await _context.SaveChangesAsync();
            return Json(new { success = true, redirectUrl = "/Teacher/Exams" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateExam(int id, [FromBody] SaveExamRequest request)
    {
        try
        {
            var exam = await _context.ExamPapers.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == id);
            if (exam == null) return NotFound(new { success = false, message = "Không tìm thấy đề thi cần sửa" });

            var teacherId = GetTeacherId();
            if (teacherId.HasValue && exam.TeacherId != teacherId.Value) return Forbid();

            exam.Title = request.Title;
            exam.Subject = request.Subject;
            exam.DurationInMinutes = request.Duration ?? exam.DurationInMinutes;
            exam.CreatedAt = DateTime.Now;
            if (!string.IsNullOrEmpty(request.Status)) {
                exam.Status = request.Status;
            }

            var questionIds = exam.Questions.Select(q => q.Id).ToList();
            var submissionDetailsToDelete = _context.Set<SubmissionDetail>().Where(s => questionIds.Contains(s.QuestionId));
            _context.Set<SubmissionDetail>().RemoveRange(submissionDetailsToDelete);
            _context.Questions.RemoveRange(exam.Questions);
            exam.Questions.Clear();

            foreach (var q in request.Questions)
            {
                exam.Questions.Add(new Question
                {
                    Content = q.Text,
                    OptionA = q.Options.FirstOrDefault(o => o.Label == "A")?.Text ?? "A",
                    OptionB = q.Options.FirstOrDefault(o => o.Label == "B")?.Text ?? "B",
                    OptionC = q.Options.FirstOrDefault(o => o.Label == "C")?.Text ?? "C",
                    OptionD = q.Options.FirstOrDefault(o => o.Label == "D")?.Text ?? "D",
                    CorrectOption = q.Options.FirstOrDefault(o => o.IsCorrect)?.Label ?? "A"
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = exam.Id, redirectUrl = "/Teacher/Exams" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetExamPreview(int examPaperId)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });

        var examData = await _context.ExamPapers.AsNoTracking()
            .Where(e => e.Id == examPaperId && e.TeacherId == teacherId.Value && e.IsDeleted == false)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.DurationInMinutes,
                Questions = e.Questions.OrderBy(q => q.Id).Select(q => new { q.Id, q.Content, q.Explanation, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectOption }).ToList()
            })
            .FirstOrDefaultAsync();

        if (examData is null) return NotFound(new { success = false, message = "Không tìm thấy đề thi hợp lệ." });

        return Json(new
        {
            success = true,
            exam = new
            {
                id = examData.Id,
                title = examData.Title,
                durationInMinutes = examData.DurationInMinutes,
                questionCount = examData.Questions.Count,
                questions = examData.Questions.Select(q => new
                {
                    id = q.Id,
                    content = q.Content,
                    explanation = q.Explanation,
                    options = new[]
                    {
                        new { key = "A", text = q.OptionA, isCorrect = q.CorrectOption == "A" },
                        new { key = "B", text = q.OptionB, isCorrect = q.CorrectOption == "B" },
                        new { key = "C", text = q.OptionC, isCorrect = q.CorrectOption == "C" },
                        new { key = "D", text = q.OptionD, isCorrect = q.CorrectOption == "D" }
                    }
                }).ToList()
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessionDetail(int sessionId)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });

        var sessionData = await _context.ExamSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.Classroom.TeacherId == teacherId.Value)
            .Select(s => new
            {
                s.Id,
                s.SessionName,
                s.ExamPaperId,
                s.ClassroomId,
                s.StartTime,
                s.EndTime,
                s.DurationInMinutes,
                s.SessionPassword,
                s.AllowViewExplanation,
                s.ShuffleQuestions,
                s.ShuffleAnswers,
                s.Notes,
                ExamTitle = s.ExamPaper.Title,
                ClassroomName = s.Classroom.ClassName,
                SubmissionCount = s.Submissions.Count
            })
            .FirstOrDefaultAsync();

        if (sessionData is null) return NotFound(new { success = false, message = "Không tìm thấy ca thi." });

        var now = DateTime.Now;
        var status = "Sắp diễn ra";
        if (now >= sessionData.StartTime && now <= sessionData.EndTime) status = "Đang diễn ra";
        else if (now > sessionData.EndTime) status = "Đã kết thúc";

        var joinLink = $"{Request.Scheme}://{Request.Host}/Student/Index?sessionId={sessionData.Id}";
        var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=270x270&data={Uri.EscapeDataString(joinLink)}";

        return Json(new
        {
            success = true,
            data = new
            {
                sessionData.Id,
                sessionData.SessionName,
                sessionData.ExamPaperId,
                sessionData.ClassroomId,
                sessionData.StartTime,
                sessionData.EndTime,
                sessionData.DurationInMinutes,
                password = sessionData.SessionPassword ?? "",
                sessionData.AllowViewExplanation,
                sessionData.ShuffleQuestions,
                sessionData.ShuffleAnswers,
                sessionData.Notes,
                examTitle = sessionData.ExamTitle,
                classroomName = sessionData.ClassroomName,
                submissionCount = sessionData.SubmissionCount,
                status,
                joinLink,
                qrUrl
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> MonitorSession(int sessionId, string? searchKeyword = null, string statusFilter = "all")
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var now = DateTime.Now;
        var sessionInfo = await _context.ExamSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.Classroom.TeacherId == teacherId.Value)
            .Select(s => new
            {
                s.Id,
                s.SessionName,
                s.ClassroomId,
                s.StartTime,
                s.EndTime,
                s.DurationInMinutes,
                ExamTitle = s.ExamPaper.Title,
                ClassName = s.Classroom.ClassName
            })
            .FirstOrDefaultAsync();

        if (sessionInfo is null) return NotFound();

        var studentMemberQuery = _context.ClassroomMembers.AsNoTracking()
            .Where(cm => cm.ClassroomId == sessionInfo.ClassroomId);

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim();
            var usedMeili = false;

            if (await IsMeiliAvailableAsync())
            {
                var searchableStudents = await studentMemberQuery
                    .Select(cm => new StudentSearchDocument
                    {
                        Id = cm.Student.Id,
                        ClassroomId = sessionInfo.ClassroomId,
                        FullName = cm.Student.FullName,
                        Email = cm.Student.Email
                    })
                    .ToListAsync();

                await _meiliSearch!.IndexClassStudentsAsync(sessionInfo.ClassroomId, searchableStudents);
                var matchedStudentIds = await _meiliSearch.SearchClassStudentIdsAsync(sessionInfo.ClassroomId, keyword);

                if (matchedStudentIds.Count > 0)
                {
                    studentMemberQuery = studentMemberQuery.Where(cm => matchedStudentIds.Contains(cm.StudentId));
                }
                else
                {
                    studentMemberQuery = studentMemberQuery.Where(_ => false);
                }

                usedMeili = true;
            }

            if (!usedMeili)
            {
                studentMemberQuery = studentMemberQuery.Where(cm =>
                    cm.Student.FullName.Contains(keyword) ||
                    cm.Student.Email.Contains(keyword));
            }
        }

        var studentRows = await studentMemberQuery
            .Select(cm => new
            {
                cm.StudentId,
                cm.Student.FullName,
                cm.Student.Email,
                Submission = cm.Student.Submissions.Where(sub => sub.ExamSessionId == sessionId).OrderByDescending(sub => sub.StartedAt).Select(sub => new
                {
                    sub.Id,
                    sub.StartedAt,
                    sub.SubmittedAt,
                    sub.Score,
                    sub.CorrectAnswersCount,
                    sub.WarningCount,
                    sub.Status
                }).FirstOrDefault()
            })
            .OrderBy(x => x.FullName)
            .ToListAsync();

        var studentItems = studentRows.Select(x =>
        {
            var submission = x.Submission;
            var statusText = MapSubmissionStatus(submission?.Status, submission?.SubmittedAt, sessionInfo.EndTime, now);
            var timeSpentMinutes = CalculateTimeSpentMinutes(submission?.StartedAt, submission?.SubmittedAt, sessionInfo.EndTime, now);

            return new SessionMonitorStudentItemVM
            {
                StudentId = x.StudentId,
                FullName = x.FullName,
                Email = x.Email,
                SubmissionId = submission?.Id,
                StartedAt = submission?.StartedAt,
                SubmittedAt = submission?.SubmittedAt,
                Score = submission?.Score,
                CorrectAnswersCount = submission?.CorrectAnswersCount,
                WarningCount = submission?.WarningCount ?? 0,
                TimeSpentMinutes = timeSpentMinutes,
                SubmissionStatus = statusText
            };
        }).ToList();

        studentItems = statusFilter switch
        {
            "submitted" => studentItems.Where(x => x.SubmissionStatus == "Đã nộp bài").ToList(),
            "in_progress" => studentItems.Where(x => x.SubmissionStatus == "Đang làm bài").ToList(),
            "not_started" => studentItems.Where(x => x.SubmissionStatus == "Chưa vào thi").ToList(),
            "late" => studentItems.Where(x => x.SubmissionStatus == "Hết giờ chưa nộp").ToList(),
            _ => studentItems
        };

        return View(new SessionMonitorPageVM
        {
            SessionId = sessionInfo.Id,
            SessionName = sessionInfo.SessionName,
            ClassName = sessionInfo.ClassName,
            ExamTitle = sessionInfo.ExamTitle,
            StartTime = sessionInfo.StartTime,
            EndTime = sessionInfo.EndTime,
            DurationInMinutes = sessionInfo.DurationInMinutes,
            SearchKeyword = searchKeyword,
            StatusFilter = statusFilter,
            Students = studentItems,
            TotalStudents = studentItems.Count,
            SubmittedCount = studentItems.Count(x => x.SubmissionStatus == "Đã nộp bài"),
            InProgressCount = studentItems.Count(x => x.SubmissionStatus == "Đang làm bài"),
            NotStartedCount = studentItems.Count(x => x.SubmissionStatus == "Chưa vào thi"),
            LateNotSubmittedCount = studentItems.Count(x => x.SubmissionStatus == "Hết giờ chưa nộp"),
            ViolationCount = studentItems.Count(x => x.WarningCount > 0)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Sessions(string? searchKeyword, string statusFilter = "all", string sortBy = "start_desc", int page = 1)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized();

        var now = DateTime.Now;
        var baseQuery = _context.ExamSessions.AsNoTracking().Where(s => s.ExamPaper.TeacherId == teacherId.Value);

        var classrooms = await _context.Classrooms.AsNoTracking()
            .Where(c => c.TeacherId == teacherId.Value && c.IsDeleted == false)
            .OrderBy(c => c.ClassName)
            .Select(c => new SessionOptionVM { Id = c.Id, Name = c.ClassName })
            .ToListAsync();

        var examPapers = await _context.ExamPapers.AsNoTracking()
            .Where(e => e.TeacherId == teacherId.Value && e.IsDeleted == false && e.Status == "Xuất bản")
            .OrderBy(e => e.Title)
            .Select(e => new SessionOptionVM { Id = e.Id, Name = e.Title, Subject = e.Subject ?? string.Empty, DurationInMinutes = e.DurationInMinutes })
            .ToListAsync();

        var subjects = await _context.ExamPapers.AsNoTracking()
            .Where(e => e.TeacherId == teacherId.Value && e.IsDeleted == false && e.Subject != null && e.Subject != "")
            .Select(e => e.Subject!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.Trim();
            var meiliAvailable = await IsMeiliAvailableAsync();
            if (meiliAvailable)
            {
                var searchableSessions = await baseQuery.Select(s => new ExamSessionSearchDocument
                {
                    Id = s.Id,
                    TeacherId = s.ExamPaper.TeacherId,
                    SessionName = s.SessionName,
                    ClassName = s.Classroom.ClassName,
                    ExamTitle = s.ExamPaper.Title
                }).ToListAsync();

                await _meiliSearch!.IndexTeacherExamSessionsAsync(teacherId.Value, searchableSessions);
                var matchedSessionIds = await _meiliSearch.SearchTeacherExamSessionIdsAsync(teacherId.Value, keyword);
                if (matchedSessionIds.Count > 0) baseQuery = baseQuery.Where(s => matchedSessionIds.Contains(s.Id));
                else baseQuery = baseQuery.Where(s => s.SessionName.Contains(keyword) || s.Classroom.ClassName.Contains(keyword) || s.ExamPaper.Title.Contains(keyword));
            }
            else
            {
                baseQuery = baseQuery.Where(s => s.SessionName.Contains(keyword) || s.Classroom.ClassName.Contains(keyword) || s.ExamPaper.Title.Contains(keyword));
            }
        }

        baseQuery = statusFilter switch
        {
            "ongoing" => baseQuery.Where(s => s.StartTime <= now && s.EndTime >= now),
            "upcoming" => baseQuery.Where(s => s.StartTime > now),
            "finished" => baseQuery.Where(s => s.EndTime < now),
            _ => baseQuery
        };

        var projectedQuery = baseQuery.Select(s => new TeacherSessionItemVM
        {
            Id = s.Id,
            SessionName = s.SessionName,
            ClassName = s.Classroom.ClassName,
            ExamTitle = s.ExamPaper.Title,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationInMinutes = s.DurationInMinutes,
            SubmissionCount = s.Submissions.Count,
            Status = s.StartTime > now ? "Sắp diễn ra" : (s.EndTime < now ? "Đã kết thúc" : "Đang diễn ra")
        });

        projectedQuery = sortBy switch
        {
            "start_asc" => projectedQuery.OrderBy(s => s.StartTime),
            "name_asc" => projectedQuery.OrderBy(s => s.SessionName),
            "submissions_desc" => projectedQuery.OrderByDescending(s => s.SubmissionCount),
            _ => projectedQuery.OrderByDescending(s => s.StartTime)
        };

        const int pageSize = 5;
        page = Math.Max(1, page);
        var totalItems = await projectedQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);

        return View(new TeacherSessionListVM
        {
            Sessions = await projectedQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Classrooms = classrooms,
            ExamPapers = examPapers,
            Subjects = subjects,
            SearchKeyword = searchKeyword,
            StatusFilter = statusFilter,
            SortBy = sortBy
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(string? sessionName, int? classroomId, int? examPaperId, DateTime? startTime, DateTime? endTime, string? sessionPassword, bool allowViewExplanation = true, bool shuffleQuestions = true, bool shuffleAnswers = true, string? notes = null)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(sessionName)) errors.Add("Trường Tên ca thi là trường bắt buộc.");
        if (!classroomId.HasValue) errors.Add("Trường Lớp học tham gia là trường bắt buộc.");
        if (!examPaperId.HasValue) errors.Add("Trường Đề thi là trường bắt buộc.");
        if (!startTime.HasValue) errors.Add("Trường Giờ bắt đầu là trường bắt buộc.");
        if (!endTime.HasValue) errors.Add("Trường Giờ kết thúc là trường bắt buộc.");

        var now = DateTime.Now;
        if (startTime.HasValue && startTime.Value < now) errors.Add("Giờ bắt đầu không được là thời gian trong quá khứ.");
        if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value) errors.Add("Giờ kết thúc phải lớn hơn giờ bắt đầu.");

        var classroomExists = classroomId.HasValue && await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == classroomId.Value && c.TeacherId == teacherId.Value && c.IsDeleted == false);
        if (classroomId.HasValue && !classroomExists) errors.Add("Lớp học đã chọn không hợp lệ.");

        var examPaperExists = examPaperId.HasValue && await _context.ExamPapers.AsNoTracking().AnyAsync(e => e.Id == examPaperId.Value && e.TeacherId == teacherId.Value && e.IsDeleted == false);
        if (examPaperId.HasValue && !examPaperExists) errors.Add("Đề thi đã chọn không hợp lệ.");

        var examPaperDuration = examPaperId.HasValue ? await _context.ExamPapers.AsNoTracking().Where(e => e.Id == examPaperId.Value && e.TeacherId == teacherId.Value && e.IsDeleted == false).Select(e => e.DurationInMinutes).FirstOrDefaultAsync() : 0;
        if (examPaperId.HasValue && examPaperDuration <= 0) errors.Add("Đề thi chưa cấu hình thời gian hợp lệ.");

        if (startTime.HasValue && endTime.HasValue && examPaperDuration > 0)
        {
            var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
            if (computedDuration < examPaperDuration) errors.Add($"Tổng thời gian ca thi phải lớn hơn hoặc bằng thời gian của đề thi ({examPaperDuration} phút).");
        }

        if (classroomId.HasValue && startTime.HasValue && endTime.HasValue && classroomExists)
        {
            var conflictingSession = await _context.ExamSessions.AsNoTracking().Where(s => s.ClassroomId == classroomId.Value && s.StartTime < endTime.Value && s.EndTime > startTime.Value).OrderBy(s => s.StartTime).Select(s => new { s.SessionName, s.StartTime, s.EndTime }).FirstOrDefaultAsync();
            if (conflictingSession is not null) errors.Add($"Lớp học đã có ca thi trùng thời gian ({conflictingSession.StartTime:dd/MM/yyyy HH:mm} - {conflictingSession.EndTime:dd/MM/yyyy HH:mm}): {conflictingSession.SessionName}.");
        }

        if (errors.Count > 0) return BadRequest(new { success = false, message = errors.First(), errors });

        var session = new ExamSession
        {
            SessionName = sessionName!.Trim(),
            ClassroomId = classroomId!.Value,
            ExamPaperId = examPaperId!.Value,
            StartTime = startTime!.Value,
            EndTime = endTime!.Value,
            DurationInMinutes = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes),
            SessionPassword = string.IsNullOrWhiteSpace(sessionPassword) ? null : sessionPassword.Trim(),
            AllowViewExplanation = allowViewExplanation,
            IsShuffled = shuffleQuestions || shuffleAnswers,
            ShuffleQuestions = shuffleQuestions,
            ShuffleAnswers = shuffleAnswers,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        _context.ExamSessions.Add(session);
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Tạo ca thi thành công.", sessionId = session.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSession(int? sessionId, string? sessionName, int? classroomId, int? examPaperId, DateTime? startTime, DateTime? endTime, string? sessionPassword, bool allowViewExplanation = true, bool shuffleQuestions = true, bool shuffleAnswers = true, string? notes = null)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
        if (!sessionId.HasValue || sessionId.Value <= 0) return BadRequest(new { success = false, message = "ID ca thi không hợp lệ." });

        var session = await _context.ExamSessions.Include(s => s.Classroom).FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.Classroom.TeacherId == teacherId.Value);
        if (session is null) return NotFound(new { success = false, message = "Không tìm thấy ca thi để cập nhật." });

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(sessionName)) errors.Add("Trường Tên ca thi là trường bắt buộc.");
        if (!examPaperId.HasValue) errors.Add("Trường Đề thi là trường bắt buộc.");
        if (!classroomId.HasValue) errors.Add("Trường Lớp học là trường bắt buộc.");
        if (!startTime.HasValue) errors.Add("Trường Giờ bắt đầu là trường bắt buộc.");
        if (!endTime.HasValue) errors.Add("Trường Giờ kết thúc là trường bắt buộc.");
        if (errors.Any()) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });

        if (endTime <= startTime) errors.Add("Giờ kết thúc phải lớn hơn giờ bắt đầu.");

        var classroomExists = classroomId.HasValue && await _context.Classrooms.AsNoTracking().AnyAsync(c => c.Id == classroomId.Value && c.TeacherId == teacherId.Value && c.IsDeleted == false);
        if (classroomId.HasValue && !classroomExists) errors.Add("Lớp học đã chọn không hợp lệ.");

        var examPaperExists = examPaperId.HasValue && await _context.ExamPapers.AsNoTracking().AnyAsync(e => e.Id == examPaperId.Value && e.TeacherId == teacherId.Value && e.IsDeleted == false);
        if (examPaperId.HasValue && !examPaperExists) errors.Add("Đề thi đã chọn không hợp lệ.");

        var examPaperDuration = examPaperId.HasValue ? await _context.ExamPapers.AsNoTracking().Where(e => e.Id == examPaperId.Value && e.TeacherId == teacherId.Value && e.IsDeleted == false).Select(e => e.DurationInMinutes).FirstOrDefaultAsync() : 0;
        if (examPaperId.HasValue && examPaperDuration <= 0) errors.Add("Đề thi chưa cấu hình thời gian hợp lệ.");

        if (startTime.HasValue && endTime.HasValue && examPaperDuration > 0)
        {
            var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
            if (computedDuration < examPaperDuration) errors.Add($"Tổng thời gian ca thi phải lớn hơn hoặc bằng thời gian của đề thi ({examPaperDuration} phút).");
        }

        if (classroomId.HasValue && startTime.HasValue && endTime.HasValue && classroomExists)
        {
            var conflictingSession = await _context.ExamSessions.AsNoTracking().Where(s => s.ClassroomId == classroomId.Value && s.Id != sessionId.Value && s.StartTime < endTime.Value && s.EndTime > startTime.Value).OrderBy(s => s.StartTime).Select(s => new { s.SessionName, s.StartTime, s.EndTime }).FirstOrDefaultAsync();
            if (conflictingSession is not null) errors.Add($"Lớp học đã có ca thi trùng thời gian ({conflictingSession.StartTime:dd/MM/yyyy HH:mm} - {conflictingSession.EndTime:dd/MM/yyyy HH:mm}): {conflictingSession.SessionName}.");
        }

        if (errors.Any()) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });

        session.SessionName = sessionName!.Trim();
        session.ClassroomId = classroomId!.Value;
        session.ExamPaperId = examPaperId!.Value;
        session.StartTime = startTime!.Value;
        session.EndTime = endTime!.Value;
        session.DurationInMinutes = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
        session.SessionPassword = string.IsNullOrWhiteSpace(sessionPassword) ? null : sessionPassword.Trim();
        session.AllowViewExplanation = allowViewExplanation;
        session.IsShuffled = shuffleQuestions || shuffleAnswers;
        session.ShuffleQuestions = shuffleQuestions;
        session.ShuffleAnswers = shuffleAnswers;
        session.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        _context.ExamSessions.Update(session);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Cập nhật ca thi thành công.", sessionId = session.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSession(int? sessionId)
    {
        var teacherId = GetTeacherId();
        if (!teacherId.HasValue) return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
        if (!sessionId.HasValue || sessionId.Value <= 0) return BadRequest(new { success = false, message = "ID ca thi không hợp lệ." });

        var session = await _context.ExamSessions.Include(s => s.Classroom).FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.Classroom.TeacherId == teacherId.Value);
        if (session is null) return NotFound(new { success = false, message = "Không tìm thấy ca thi để xóa." });
        if (session.StartTime <= DateTime.Now) return BadRequest(new { success = false, message = "Chỉ có thể xóa ca thi chưa bắt đầu." });

        _context.ExamSessions.Remove(session);
        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Xóa ca thi thành công." });
    }

    private async Task<bool> IsMeiliAvailableAsync()
    {
        if (_meiliSearch is null)
        {
            return false;
        }

        try
        {
            return await _meiliSearch.IsAvailableAsync();
        }
        catch
        {
            return false;
        }
    }

    private int? GetTeacherId()
    {
        var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(teacherIdStr, out var teacherId) ? teacherId : null;
    }

    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code1 = new string(Enumerable.Range(0, 3).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        var code2 = new string(Enumerable.Range(0, 3).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        var code3 = new string(Enumerable.Range(0, 3).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        return $"{code1}-{code2}-{code3}";
    }

    private static string MapSubmissionStatus(int? rawStatus, DateTime? submittedAt, DateTime sessionEndTime, DateTime now)
    {
        if (submittedAt.HasValue) return "Đã nộp bài";
        if (!rawStatus.HasValue) return now > sessionEndTime ? "Hết giờ chưa nộp" : "Chưa vào thi";
        if (rawStatus.Value == 1) return "Đã nộp bài";
        return now > sessionEndTime ? "Hết giờ chưa nộp" : "Đang làm bài";
    }

    private static int? CalculateTimeSpentMinutes(DateTime? startedAt, DateTime? submittedAt, DateTime sessionEndTime, DateTime now)
    {
        if (!startedAt.HasValue) return null;
        var end = submittedAt ?? (now < sessionEndTime ? now : sessionEndTime);
        if (end <= startedAt.Value) return 0;
        return (int)Math.Ceiling((end - startedAt.Value).TotalMinutes);
    }
}

public class SaveExamRequest
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Bản nháp";
    public int? Duration { get; set; }
    public List<QuestionRequest> Questions { get; set; } = new();
}

public class QuestionRequest
{
    public string Number { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<OptionRequest> Options { get; set; } = new();
}

public class OptionRequest
{
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
