using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OnlineExam.Models;
using OnlineExam.Services.Search;
using OnlineExam.ViewModels;
using System.Security.Claims;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OnlineExam.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly OnlineExamDbContext _context;
        private readonly IMeiliSearchService? _meiliSearch;

        public StudentController(OnlineExamDbContext context, IMeiliSearchService? meiliSearch = null)
        {
            _context = context;
            _meiliSearch = meiliSearch;
        }

        private void AutoSubmitExpiredExams(User student)
        {
            var now = DateTime.Now;
            var expiredPendingSubmissions = _context.Submissions
                .Include(s => s.ExamSession)
                .Where(s => s.StudentId == student.Id && s.Status == 0)
                .ToList();

            bool changed = false;
            foreach (var sub in expiredPendingSubmissions)
            {
                var session = sub.ExamSession;
                var submissionEndTime = sub.StartedAt.AddMinutes(session.DurationInMinutes);
                var actualEndTime = submissionEndTime < session.EndTime ? submissionEndTime : session.EndTime;

                if (now > actualEndTime)
                {
                    sub.Status = 2; // Auto force-submitted
                    sub.SubmittedAt = actualEndTime;
                    sub.CorrectAnswersCount = 0;
                    sub.Score = 0;
                    changed = true;
                }
            }

            if (changed)
            {
                _context.SaveChanges();
            }
        }

        public IActionResult Index()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (student == null) return RedirectToAction("Login", "Auth");

            AutoSubmitExpiredExams(student);

            var now = DateTime.Now;

            // Lấy các lớp mà sinh viên đang tham gia
            var joinedClassroomIds = _context.ClassroomMembers
                .Where(cm => cm.StudentId == student.Id)
                .Select(cm => cm.ClassroomId)
                .ToList();

            // Lấy các ca thi thuộc các lớp đó
            var examSessions = _context.ExamSessions
                .Include(es => es.Classroom)
                .ThenInclude(c => c.Teacher)
                .Include(es => es.ExamPaper)
                .Where(es => joinedClassroomIds.Contains(es.ClassroomId))
                .ToList();

            // Lấy các bài đã nộp của sinh viên này
            var submittedSessionIds = _context.Submissions
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2))
                .Select(s => s.ExamSessionId)
                .ToList();

            var submissions = _context.Submissions
                .Where(s => s.StudentId == student.Id)
                .ToList();

            var sessionLookup = examSessions.ToDictionary(s => s.Id);
            var completedSubmissions = submissions
                .Where(s => (s.Status == 1 || s.Status == 2) && s.Score.HasValue && sessionLookup.ContainsKey(s.ExamSessionId))
                .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                .ToList();

            var examItems = new List<StudentExamItemVM>();

            foreach (var session in examSessions)
            {
                var isSubmitted = submittedSessionIds.Contains(session.Id);
                var item = new StudentExamItemVM
                {
                    ExamSessionId = session.Id,
                    Title = session.SessionName,
                    TeacherName = session.Classroom.Teacher.FullName,
                    Duration = session.DurationInMinutes,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    ClassName = session.Classroom.ClassName,
                    ClassroomId = session.Classroom.Id,
                    IsSubmitted = isSubmitted
                };
                examItems.Add(item);
            }

            var pendingExamsCount = examItems.Count(e => !e.IsSubmitted && e.EndTime > now);

            var joinedClasses = _context.Classrooms
                .Where(c => joinedClassroomIds.Contains(c.Id))
                .Select(c => new ClassroomVM { Id = c.Id, ClassName = c.ClassName })
                .ToList();

            var scoreHistory = _context.Submissions
                .Include(s => s.ExamSession)
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2) && s.Score != null)
                .OrderBy(s => s.SubmittedAt)
                .Select(s => new StudentScoreChartItemVM
                {
                    ExamName = s.ExamSession.SessionName,
                    Score = s.Score ?? 0,
                    SubmittedAt = s.SubmittedAt ?? DateTime.Now
                })
                .ToList();

            var vm = new StudentDashboardVM
            {
                StudentName = student.FullName,
                PendingExamsCount = pendingExamsCount,
                TotalExamsCount = examItems.Count,
                CompletedExamsCount = completedSubmissions.Select(s => s.ExamSessionId).Distinct().Count(),
                AverageScore = completedSubmissions.Any() ? completedSubmissions.Average(s => s.Score ?? 0) : 0,
                Exams = examItems.OrderByDescending(e => e.StartTime).ToList(),
                JoinedClasses = joinedClasses,
                ScoreHistory = scoreHistory,
                RecentResults = completedSubmissions
                    .Take(5)
                    .Select(s =>
                    {
                        var session = sessionLookup[s.ExamSessionId];
                        return new StudentRecentResultVM
                        {
                            ExamSessionId = s.ExamSessionId,
                            ExamName = session.SessionName,
                            ClassName = session.Classroom.ClassName,
                            Score = s.Score ?? 0,
                            SubmittedAt = s.SubmittedAt ?? DateTime.Now
                        };
                    })
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> JoinClass(string? searchKeyword, string sortBy = "joined_desc", int page = 1)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            page = Math.Max(1, page);
            const int pageSize = 6;

            var joinedClassQuery = _context.ClassroomMembers
                .AsNoTracking()
                .Where(cm => cm.StudentId == student.Id && cm.Classroom.IsDeleted != true)
                .Select(cm => new
                {
                    cm.ClassroomId,
                    cm.Classroom.ClassName,
                    TeacherName = cm.Classroom.Teacher.FullName,
                    cm.Classroom.JoinCode,
                    cm.JoinedAt
                });

            var keyword = searchKeyword?.Trim();
            var showMeiliWarning = false;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var usedMeili = false;
                if (await IsMeiliAvailableAsync())
                {
                    var searchableClasses = await joinedClassQuery
                        .Select(x => new StudentClassroomSearchDocument
                        {
                            Id = x.ClassroomId,
                            ClassName = x.ClassName,
                            TeacherName = x.TeacherName,
                            JoinCode = x.JoinCode
                        })
                        .ToListAsync();

                    await _meiliSearch!.IndexStudentClassroomsAsync(student.Id, searchableClasses);
                    var matchedClassIds = await _meiliSearch.SearchStudentClassroomIdsAsync(student.Id, keyword!);
                    if (matchedClassIds.Count > 0)
                    {
                        joinedClassQuery = joinedClassQuery.Where(x => matchedClassIds.Contains(x.ClassroomId));
                    }
                    else
                    {
                        joinedClassQuery = joinedClassQuery.Where(_ => false);
                    }

                    usedMeili = true;
                }

                if (!usedMeili)
                {
                    showMeiliWarning = true;
                    joinedClassQuery = joinedClassQuery.Where(x =>
                        x.ClassName.Contains(keyword!) ||
                        x.TeacherName.Contains(keyword!) ||
                        x.JoinCode.Contains(keyword!));
                }
            }

            joinedClassQuery = sortBy switch
            {
                "joined_asc" => joinedClassQuery.OrderBy(x => x.JoinedAt),
                "name_asc" => joinedClassQuery.OrderBy(x => x.ClassName),
                "name_desc" => joinedClassQuery.OrderByDescending(x => x.ClassName),
                "exam_desc" => joinedClassQuery.OrderByDescending(x => _context.ExamSessions.Count(s => s.ClassroomId == x.ClassroomId)),
                "exam_asc" => joinedClassQuery.OrderBy(x => _context.ExamSessions.Count(s => s.ClassroomId == x.ClassroomId)),
                _ => joinedClassQuery.OrderByDescending(x => x.JoinedAt)
            };

            var totalItems = await joinedClassQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);

            var joinedClassPage = await joinedClassQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var classroomIds = joinedClassPage.Select(x => x.ClassroomId).ToList();
            var sessions = await _context.ExamSessions
                .AsNoTracking()
                .Where(s => classroomIds.Contains(s.ClassroomId))
                .Select(s => new
                {
                    s.Id,
                    s.ClassroomId,
                    s.SessionName,
                    s.StartTime,
                    s.EndTime,
                    s.AllowViewExplanation,
                    s.Classroom.ClassName
                })
                .ToListAsync();

            var vm = new StudentJoinClassVM
            {
                StudentName = student.FullName,
                JoinedClasses = joinedClassPage.Select(item =>
                {
                    var classSessions = sessions.Where(s => s.ClassroomId == item.ClassroomId)
                        .OrderByDescending(s => s.StartTime)
                        .ToList();

                    return new StudentJoinedClassItemVM
                    {
                        ClassroomId = item.ClassroomId,
                        ClassName = item.ClassName,
                        TeacherName = item.TeacherName,
                        JoinCode = item.JoinCode,
                        JoinedAt = item.JoinedAt,
                        ExamCount = classSessions.Count,
                        RecentExams = classSessions.Take(3).Select(s => new StudentJoinedClassExamVM
                        {
                            SessionId = s.Id,
                            SessionName = s.SessionName,
                            StartTime = s.StartTime,
                            EndTime = s.EndTime,
                            Status = DateTime.Now < s.StartTime ? "Chưa mở" : (DateTime.Now > s.EndTime ? "Đã đóng" : "Đang mở")
                        }).ToList()
                    };
                }).OrderByDescending(c => c.JoinedAt).ToList()
            };

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.SortBy = sortBy;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.ShowMeiliSearchWarning = showMeiliWarning;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> MyExams(string? searchKeyword, string statusFilter = "all", int page = 1)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            AutoSubmitExpiredExams(student);

            var now = DateTime.Now;
            page = Math.Max(1, page);
            const int pageSize = 6;

            var joinedClassIds = await _context.ClassroomMembers
                .Where(cm => cm.StudentId == student.Id)
                .Select(cm => cm.ClassroomId)
                .ToListAsync();

            var joinedClasses = await _context.Classrooms
                .Where(c => joinedClassIds.Contains(c.Id) && c.IsDeleted != true)
                .Select(c => new ClassroomVM { Id = c.Id, ClassName = c.ClassName })
                .ToListAsync();

            var sessionQuery = _context.ExamSessions
                .AsNoTracking()
                .Include(s => s.Classroom)
                .ThenInclude(c => c.Teacher)
                .Include(s => s.ExamPaper)
                .Where(s => joinedClassIds.Contains(s.ClassroomId));

            var keyword = searchKeyword?.Trim();
            var showMeiliWarning = false;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var usedMeili = false;
                if (await IsMeiliAvailableAsync())
                {
                    var searchableSessions = await sessionQuery
                        .Select(s => new StudentExamSessionSearchDocument
                        {
                            Id = s.Id,
                            SessionName = s.SessionName,
                            ClassName = s.Classroom.ClassName,
                            TeacherName = s.Classroom.Teacher.FullName
                        })
                        .ToListAsync();

                    await _meiliSearch!.IndexStudentExamSessionsAsync(student.Id, searchableSessions);
                    var matchedSessionIds = await _meiliSearch.SearchStudentExamSessionIdsAsync(student.Id, keyword!);
                    if (matchedSessionIds.Count > 0)
                    {
                        sessionQuery = sessionQuery.Where(s => matchedSessionIds.Contains(s.Id));
                    }
                    else
                    {
                        sessionQuery = sessionQuery.Where(_ => false);
                    }

                    usedMeili = true;
                }

                if (!usedMeili)
                {
                    showMeiliWarning = true;
                    sessionQuery = sessionQuery.Where(s =>
                        s.SessionName.Contains(keyword!) ||
                        s.Classroom.ClassName.Contains(keyword!) ||
                        s.Classroom.Teacher.FullName.Contains(keyword!));
                }
            }

            var sessions = await sessionQuery.ToListAsync();

            var submissions = await _context.Submissions
                .Where(s => s.StudentId == student.Id)
                .ToListAsync();

            var examItems = new List<StudentExamItemVM>();

            foreach (var session in sessions)
            {
                var sub = submissions.FirstOrDefault(s => s.ExamSessionId == session.Id);
                var isSubmitted = sub != null && (sub.Status == 1 || sub.Status == 2);

                examItems.Add(new StudentExamItemVM
                {
                    ExamSessionId = session.Id,
                    ClassroomId = session.ClassroomId,
                    Title = session.SessionName,
                    ClassName = session.Classroom.ClassName,
                    TeacherName = session.Classroom.Teacher?.FullName ?? "Unknown",
                    Duration = session.DurationInMinutes,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    IsSubmitted = isSubmitted
                });
            }

            if (!string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                examItems = examItems.Where(e =>
                {
                    var status = e.Status;
                    return statusFilter switch
                    {
                        "Chưa làm" => status == "Chưa làm",
                        "Sắp hết hạn" => status == "Sắp hết hạn",
                        "Đã nộp" => status == "Đã nộp",
                        "Đã đóng" => status == "Đã đóng" || status == "Chưa mở",
                        _ => true
                    };
                }).ToList();
            }

            var orderedItems = examItems.OrderByDescending(e => e.StartTime).ToList();
            var totalItems = orderedItems.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);

            var pagedItems = orderedItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new StudentDashboardVM
            {
                StudentName = student.FullName,
                Exams = pagedItems,
                JoinedClasses = joinedClasses
            };

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.ShowMeiliSearchWarning = showMeiliWarning;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Results(string? searchKeyword, string sortBy = "submitted_desc", int page = 1)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            AutoSubmitExpiredExams(student);
            page = Math.Max(1, page);
            const int pageSize = 5;

            var submittedExamQuery = _context.Submissions
                .AsNoTracking()
                .Include(s => s.ExamSession)
                .Include(s => s.ExamSession.Classroom)
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2))
                .AsQueryable();

            var keyword = searchKeyword?.Trim();
            var showMeiliWarning = false;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var usedMeili = false;
                if (await IsMeiliAvailableAsync())
                {
                    var searchableSubmissions = await submittedExamQuery
                        .Select(s => new StudentSubmissionSearchDocument
                        {
                            Id = s.Id,
                            ExamName = s.ExamSession.SessionName,
                            ClassName = s.ExamSession.Classroom.ClassName,
                            Score = s.Score,
                            SubmittedAt = s.SubmittedAt
                        })
                        .ToListAsync();

                    await _meiliSearch!.IndexStudentSubmissionsAsync(student.Id, searchableSubmissions);
                    var matchedSubmissionIds = await _meiliSearch.SearchStudentSubmissionIdsAsync(student.Id, keyword!);
                    if (matchedSubmissionIds.Count > 0)
                    {
                        submittedExamQuery = submittedExamQuery.Where(s => matchedSubmissionIds.Contains(s.Id));
                    }
                    else
                    {
                        submittedExamQuery = submittedExamQuery.Where(_ => false);
                    }

                    usedMeili = true;
                }

                if (!usedMeili)
                {
                    showMeiliWarning = true;
                    submittedExamQuery = submittedExamQuery.Where(s =>
                        s.ExamSession.SessionName.Contains(keyword!) ||
                        s.ExamSession.Classroom.ClassName.Contains(keyword!));
                }
            }

            submittedExamQuery = sortBy switch
            {
                "submitted_asc" => submittedExamQuery.OrderBy(s => s.SubmittedAt),
                "score_desc" => submittedExamQuery.OrderByDescending(s => s.Score),
                "score_asc" => submittedExamQuery.OrderBy(s => s.Score),
                _ => submittedExamQuery.OrderByDescending(s => s.SubmittedAt)
            };

            var totalItems = await submittedExamQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);

            var submittedExams = await submittedExamQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var scoreHistory = await _context.Submissions
                .AsNoTracking()
                .Include(s => s.ExamSession)
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2) && s.Score != null)
                .Where(s => s.Score != null)
                .OrderBy(s => s.SubmittedAt)
                .Select(s => new StudentScoreChartItemVM
                {
                    ExamName = s.ExamSession.SessionName,
                    Score = s.Score ?? 0,
                    SubmittedAt = s.SubmittedAt ?? DateTime.Now
                })
                .ToListAsync();

            var vm = new StudentResultsVM
            {
                StudentName = student.FullName,
                Submissions = submittedExams,
                ScoreHistory = scoreHistory
            };

            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.SortBy = sortBy;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.ShowMeiliSearchWarning = showMeiliWarning;

            return View(vm);
        }

        [HttpPost]
        public IActionResult JoinClass(string joinCode)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (student == null) return Json(new { success = false, message = "Không tìm thấy thông tin sinh viên." });

            var classroom = _context.Classrooms.FirstOrDefault(c => c.JoinCode == joinCode && c.IsDeleted != true);
            if (classroom == null)
            {
                return Json(new { success = false, message = "Mã lớp không hợp lệ hoặc lớp đã bị xóa." });
            }

            var isAlreadyJoined = _context.ClassroomMembers.Any(cm => cm.ClassroomId == classroom.Id && cm.StudentId == student.Id);
            if (isAlreadyJoined)
            {
                return Json(new { success = false, message = "Bạn đã tham gia lớp này rồi." });
            }

            var member = new ClassroomMember
            {
                ClassroomId = classroom.Id,
                StudentId = student.Id,
                JoinedAt = DateTime.Now
            };

            _context.ClassroomMembers.Add(member);
            _context.SaveChanges();

            return Json(new { success = true, message = "Tham gia lớp thành công!" });
        }

        [HttpGet]
        public IActionResult TakeExam(int id)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            var session = _context.ExamSessions
                .Include(s => s.Classroom)
                .Include(s => s.ExamPaper)
                .FirstOrDefault(s => s.Id == id);

            if (session == null) return NotFound("Không tìm thấy ca thi.");

            var isMember = _context.ClassroomMembers.Any(cm => cm.ClassroomId == session.ClassroomId && cm.StudentId == student.Id);
            if (!isMember) return Forbid();

            AutoSubmitExpiredExams(student);

            // Enforce session password: redirect to password entry if a password is set
            var accessKey = $"ExamAccess_{id}";
            var hasAccess = TempData[accessKey] as string == "1";
            // If there's a password and the student hasn't been granted access, redirect to password prompt
            if (!string.IsNullOrEmpty(session.SessionPassword) && !hasAccess)
            {
                return RedirectToAction("EnterExamPassword", new { sessionId = id });
            }

            var submission = _context.Submissions.FirstOrDefault(s => s.ExamSessionId == id && s.StudentId == student.Id);
            if (submission != null && (submission.Status == 1 || submission.Status == 2))
            {
                return RedirectToAction("ExamResult", new { id = id });
            }

            var now = DateTime.Now;
            if (now < session.StartTime || now > session.EndTime)
            {
                return BadRequest("Ca thi chưa mở hoặc đã kết thúc.");
            }

            if (submission == null)
            {
                submission = new Submission
                {
                    ExamSessionId = session.Id,
                    StudentId = student.Id,
                    StartedAt = now,
                    Status = 0,
                    WarningCount = 0
                };
                _context.Submissions.Add(submission);
                _context.SaveChanges();
            }

            var questions = _context.Questions.Where(q => q.ExamPaperId == session.ExamPaperId).ToList();

            if (session.IsShuffled == true)
            {
                var random = new Random(student.Id + session.Id);
                questions = questions.OrderBy(q => random.Next()).ToList();
            }

            var vm = new TakeExamVM
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                ClassName = session.Classroom.ClassName,
                DurationInMinutes = session.DurationInMinutes,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                IsShuffled = session.IsShuffled ?? false,
                Questions = questions.Select(q => new ExamQuestionVM
                {
                    Id = q.Id,
                    Content = q.Content,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                }).ToList()
            };

            var submissionEndTime = submission.StartedAt.AddMinutes(session.DurationInMinutes);
            vm.EndTime = submissionEndTime < session.EndTime ? submissionEndTime : session.EndTime;

            return View(vm);
        }

        [HttpGet]
        public IActionResult EnterExamPassword(int sessionId)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            var session = _context.ExamSessions.Include(s => s.Classroom).FirstOrDefault(s => s.Id == sessionId);
            if (session == null) return NotFound("Không tìm thấy ca thi.");

            var isMember = _context.ClassroomMembers.Any(cm => cm.ClassroomId == session.ClassroomId && cm.StudentId == student.Id);
            if (!isMember) return Forbid();

            // If no password configured, redirect directly
            if (string.IsNullOrEmpty(session.SessionPassword)) return RedirectToAction("TakeExam", new { id = sessionId });

            ViewData["SessionId"] = sessionId;
            ViewData["SessionName"] = session.SessionName;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EnterExamPassword(int sessionId, string password)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            var session = _context.ExamSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session == null) return NotFound("Không tìm thấy ca thi.");

            var isMember = _context.ClassroomMembers.Any(cm => cm.ClassroomId == session.ClassroomId && cm.StudentId == student.Id);
            if (!isMember) return Forbid();

            if (string.IsNullOrEmpty(session.SessionPassword)) return RedirectToAction("TakeExam", new { id = sessionId });

            if (password != session.SessionPassword)
            {
                ViewData["SessionId"] = sessionId;
                ViewData["SessionName"] = session.SessionName;
                ViewData["Error"] = "Mật khẩu không đúng.";
                return View();
            }

            // Grant temporary access via TempData for the next redirect
            TempData[$"ExamAccess_{sessionId}"] = "1";
            return RedirectToAction("TakeExam", new { id = sessionId });
        }

        [HttpPost]
        public IActionResult SubmitExam([FromBody] SubmitExamDTO dto)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return Json(new { success = false, message = "Lỗi xác thực." });

            var submission = _context.Submissions
                .FirstOrDefault(s => s.ExamSessionId == dto.SessionId && s.StudentId == student.Id);

            if (submission == null || (submission.Status == 1 || submission.Status == 2))
            {
                return Json(new { success = false, message = "Bài thi đã được nộp hoặc chưa bắt đầu." });
            }

            var session = _context.ExamSessions.FirstOrDefault(s => s.Id == dto.SessionId);
            if (session == null) return Json(new { success = false, message = "Không tìm thấy ca thi." });
            var questions = _context.Questions.Where(q => q.ExamPaperId == session.ExamPaperId).ToList();

            int correctCount = 0;
            var details = new List<SubmissionDetail>();

            foreach (var q in questions)
            {
                var answer = dto.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                var selectedOption = answer?.SelectedOption;

                bool isCorrect = false;
                if (!string.IsNullOrEmpty(selectedOption) && selectedOption == q.CorrectOption.ToString())
                {
                    isCorrect = true;
                    correctCount++;
                }

                details.Add(new SubmissionDetail
                {
                    SubmissionId = submission.Id,
                    QuestionId = q.Id,
                    SelectedOption = string.IsNullOrEmpty(selectedOption) ? null : selectedOption,
                    IsCorrect = isCorrect
                });
            }

            _context.SubmissionDetails.AddRange(details);

            submission.SubmittedAt = DateTime.Now;
            submission.Status = dto.IsForceSubmit ? 2 : 1;
            submission.CorrectAnswersCount = correctCount;
            submission.Score = Math.Round((double)correctCount / questions.Count * 10, 2);
            submission.WarningCount += dto.WarningCount; // Add UI warnings

            _context.SaveChanges();

            return Json(new { success = true, redirectUrl = $"/Student/ExamResult/{dto.SessionId}" });
        }

        [HttpGet]
        public IActionResult ExamResult(int id)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            var session = _context.ExamSessions
                .Include(s => s.Classroom)
                .FirstOrDefault(s => s.Id == id);

            if (session == null) return NotFound("Không tìm thấy ca thi.");

            var submission = _context.Submissions
                .Include(s => s.SubmissionDetails)
                .FirstOrDefault(s => s.ExamSessionId == id && s.StudentId == student.Id);

            if (submission == null || (submission.Status != 1 && submission.Status != 2))
            {
                return RedirectToAction("Index"); // Chưa nộp
            }

            var questions = _context.Questions.Where(q => q.ExamPaperId == session.ExamPaperId).ToList();

            var scoreHistory = _context.Submissions
                .Include(s => s.ExamSession)
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2) && s.Score != null)
                .OrderBy(s => s.SubmittedAt)
                .Select(s => new StudentScoreChartItemVM
                {
                    ExamName = s.ExamSession.SessionName,
                    Score = s.Score ?? 0,
                    SubmittedAt = s.SubmittedAt ?? DateTime.Now
                })
                .ToList();

            var vm = new ExamResultVM
            {
                SessionId = session.Id,
                SessionName = session.SessionName,
                ClassName = session.Classroom.ClassName,
                Score = submission.Score,
                CorrectAnswersCount = submission.CorrectAnswersCount,
                TotalQuestions = questions.Count,
                SubmittedAt = submission.SubmittedAt,
                WarningCount = submission.WarningCount ?? 0,
                AllowViewScore = session.AllowViewScore ?? true,
                AllowViewExplanation = session.AllowViewExplanation ?? true,
                ScoreHistory = scoreHistory
            };

            if (vm.AllowViewScore)
            {
                var detailsList = new List<ExamResultDetailVM>();
                foreach (var q in questions)
                {
                    var ans = submission.SubmissionDetails.FirstOrDefault(d => d.QuestionId == q.Id);
                    detailsList.Add(new ExamResultDetailVM
                    {
                        QuestionId = q.Id,
                        Content = q.Content,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        // Only reveal the correct answer if teacher has enabled "Công bố lời giải"
                        CorrectOption = vm.AllowViewExplanation ? q.CorrectOption : null,
                        Explanation = vm.AllowViewExplanation ? q.Explanation : null,
                        SelectedOption = ans?.SelectedOption ?? string.Empty,
                        IsCorrect = ans?.IsCorrect ?? false
                    });
                }
                vm.Details = detailsList;
            }

            return View(vm);
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
    }
}