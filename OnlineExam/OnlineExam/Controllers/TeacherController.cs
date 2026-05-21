using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OnlineExam.ViewModels;
using OnlineExam.Models;
using OnlineExam.Services.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Claims;

namespace OnlineExam.Controllers // Chú ý: Đổi tên namespace cho khớp với project của bạn
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private readonly OnlineExamDbContext _context;
        private readonly IMeiliSearchService _meiliSearch;

        public TeacherController(OnlineExamDbContext context, IMeiliSearchService meiliSearch)
        {
            _context = context;
            _meiliSearch = meiliSearch;
        }

        // Hàm này sẽ bắt đường dẫn /Teacher/Index
        public async Task<IActionResult> Index()
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var now = DateTime.Now;

            var teacher = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == teacherId)
                .Select(u => new { u.FullName })
                .FirstOrDefaultAsync();

            if (teacher is null)
            {
                return Unauthorized();
            }

            var totalClasses = await _context.Classrooms
                .AsNoTracking()
                .CountAsync(c => c.TeacherId == teacherId && c.IsDeleted == false);

            var totalExams = await _context.ExamPapers
                .AsNoTracking()
                .CountAsync(e => e.TeacherId == teacherId && e.IsDeleted == false);

            var ongoingSessions = await _context.ExamSessions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ExamPaper.TeacherId == teacherId &&
                    s.StartTime <= now &&
                    s.EndTime >= now);

            var upcomingSessions = await _context.ExamSessions
                .AsNoTracking()
                .CountAsync(s =>
                    s.ExamPaper.TeacherId == teacherId &&
                    s.StartTime > now);

            var recentSessions = await _context.ExamSessions
                .AsNoTracking()
                .Where(s => s.ExamPaper.TeacherId == teacherId)
                .OrderByDescending(s => s.StartTime)
                .Take(8)
                .Select(s => new SessionItemVM
                {
                    SessionName = s.SessionName,
                    ClassName = s.Classroom.ClassName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                })
                .ToListAsync();

            var dashboardData = new TeacherDashboardVM
            {
                TeacherName = string.IsNullOrWhiteSpace(teacher.FullName) ? "Giảng viên" : teacher.FullName,
                TotalClasses = totalClasses,
                TotalExams = totalExams,
                OngoingSessions = ongoingSessions,
                UpcomingSessions = upcomingSessions,
                RecentSessions = recentSessions,
                SessionsToday = await _context.ExamSessions
                    .AsNoTracking()
                    .CountAsync(s => s.ExamPaper.TeacherId == teacherId && s.StartTime.Date == now.Date)
            };

            return View(dashboardData);
        }

        [HttpGet]
        public async Task<IActionResult> Classes(string? searchKeyword, string studentFilter = "all", string sortBy = "created_desc", int page = 1)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var baseQuery = _context.Classrooms
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.IsDeleted == false);

            var showMeiliWarning = false;

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                showMeiliWarning = !await _meiliSearch.IsAvailableAsync();

                var searchableClasses = await baseQuery
                    .Select(c => new ClassroomSearchDocument
                    {
                        Id = c.Id,
                        TeacherId = c.TeacherId,
                        ClassName = c.ClassName,
                        JoinCode = c.JoinCode
                    })
                    .ToListAsync();

                await _meiliSearch.IndexTeacherClassroomsAsync(teacherId, searchableClasses);
                var matchedClassroomIds = await _meiliSearch.SearchTeacherClassroomIdsAsync(teacherId, searchKeyword);

                if (matchedClassroomIds.Count > 0)
                {
                    baseQuery = baseQuery.Where(c => matchedClassroomIds.Contains(c.Id));
                }
                else
                {
                    baseQuery = baseQuery.Where(c =>
                        c.ClassName.Contains(searchKeyword) ||
                        c.JoinCode.Contains(searchKeyword));
                }
            }

            var projectedQuery = baseQuery
                .Select(c => new ClassroomVM
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

            if (page < 1)
            {
                page = 1;
            }

            var totalItems = await projectedQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var classList = await projectedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pageVm = new ClassroomListPageVM
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
            };

            return View(pageVm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClass(string className)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                return Json(new { success = false, message = "Tên lớp học không được để trống!" });
            }

            try
            {
                string joinCode = GenerateJoinCode();

                while (await _context.Classrooms.AnyAsync(c => c.JoinCode == joinCode))
                {
                    joinCode = GenerateJoinCode();
                }

                var classroom = new Classroom
                {
                    ClassName = className.Trim(),
                    JoinCode = joinCode,
                    TeacherId = teacherId,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Classrooms.Add(classroom);
                await _context.SaveChangesAsync();

                await _meiliSearch.IndexTeacherClassroomsAsync(teacherId, new[]
                {
                    new ClassroomSearchDocument
                    {
                        Id = classroom.Id,
                        TeacherId = teacherId,
                        ClassName = classroom.ClassName,
                        JoinCode = classroom.JoinCode
                    }
                });

                return Json(new 
                { 
                    success = true, 
                    message = "Tạo lớp học thành công!",
                    joinCode = joinCode,
                    classroomId = classroom.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateClass(int classId, string className)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            if (classId <= 0)
            {
                return Json(new { success = false, message = "Lớp học không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                return Json(new { success = false, message = "Tên lớp học không được để trống." });
            }

            var classroom = await _context.Classrooms
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId && c.IsDeleted == false);

            if (classroom is null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học để cập nhật." });
            }

            classroom.ClassName = className.Trim();
            await _context.SaveChangesAsync();

            await _meiliSearch.IndexTeacherClassroomsAsync(teacherId, new[]
            {
                new ClassroomSearchDocument
                {
                    Id = classroom.Id,
                    TeacherId = teacherId,
                    ClassName = classroom.ClassName,
                    JoinCode = classroom.JoinCode
                }
            });

            return Json(new
            {
                success = true,
                message = "Cập nhật lớp học thành công.",
                classroomId = classroom.Id,
                className = classroom.ClassName
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteClass(int classId)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            if (classId <= 0)
            {
                return Json(new { success = false, message = "Lớp học không hợp lệ." });
            }

            var classroom = await _context.Classrooms
                .FirstOrDefaultAsync(c => c.Id == classId && c.TeacherId == teacherId && c.IsDeleted == false);

            if (classroom is null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học để xóa." });
            }

            classroom.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa lớp học thành công." });
        }

        [HttpGet]
        public async Task<IActionResult> ManageClass(int id, string? searchKeyword, int page = 1, int pageSize = 10)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            if (page < 1)
            {
                page = 1;
            }

            pageSize = Math.Clamp(pageSize, 5, 50);

            var classroom = await _context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId && c.IsDeleted == false);

            if (classroom is null)
            {
                return NotFound();
            }

            var studentQuery = _context.ClassroomMembers
                .AsNoTracking()
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
                var searchableStudents = await _context.ClassroomMembers
                    .AsNoTracking()
                    .Where(cm => cm.ClassroomId == id)
                    .Select(cm => new StudentSearchDocument
                    {
                        Id = cm.Student.Id,
                        ClassroomId = id,
                        FullName = cm.Student.FullName,
                        Email = cm.Student.Email
                    })
                    .ToListAsync();

                await _meiliSearch.IndexClassStudentsAsync(id, searchableStudents);
                var matchedStudentIds = await _meiliSearch.SearchClassStudentIdsAsync(id, searchKeyword);

                if (matchedStudentIds.Count > 0)
                {
                    studentQuery = studentQuery.Where(s => matchedStudentIds.Contains(s.StudentId));
                }
                else
                {
                    studentQuery = studentQuery.Where(s =>
                        s.FullName.Contains(searchKeyword) ||
                        s.Email.Contains(searchKeyword));
                }
            }

            var totalItems = await studentQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var students = await studentQuery
                .OrderBy(s => s.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new ClassroomManageVM
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
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudentFromClass(int id, int studentId, string? searchKeyword, int page = 1, int pageSize = 10)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var isTeacherClassroom = await _context.Classrooms
                .AsNoTracking()
                .AnyAsync(c => c.Id == id && c.TeacherId == teacherId && c.IsDeleted == false);

            if (!isTeacherClassroom)
            {
                return NotFound();
            }

            var member = await _context.ClassroomMembers
                .FirstOrDefaultAsync(cm => cm.ClassroomId == id && cm.StudentId == studentId);

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
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var isTeacherClassroom = await _context.Classrooms
                .AsNoTracking()
                .AnyAsync(c => c.Id == id && c.TeacherId == teacherId && c.IsDeleted == false);

            if (!isTeacherClassroom)
            {
                return NotFound();
            }

            var selectedIds = studentIds?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (selectedIds.Count == 0)
            {
                TempData["ToastMessage"] = "Bạn chưa chọn học sinh để xóa.";
                TempData["ToastType"] = "error";

                return RedirectToAction(nameof(ManageClass), new { id, searchKeyword, page, pageSize });
            }

            var members = await _context.ClassroomMembers
                .Where(cm => cm.ClassroomId == id && selectedIds.Contains(cm.StudentId))
                .ToListAsync();

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

        [HttpGet]
        public async Task<IActionResult> Exams(string? searchKeyword, string sortBy = "created_desc")
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var baseQuery = _context.ExamPapers
                .AsNoTracking()
                .Where(e => e.TeacherId == teacherId && e.IsDeleted == false);

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var searchableExams = await baseQuery
                    .Select(e => new ExamPaperSearchDocument
                    {
                        Id = e.Id,
                        TeacherId = e.TeacherId,
                        Title = e.Title
                    })
                    .ToListAsync();

                await _meiliSearch.IndexTeacherExamPapersAsync(teacherId, searchableExams);
                var matchedExamIds = await _meiliSearch.SearchTeacherExamPaperIdsAsync(teacherId, searchKeyword);

                if (matchedExamIds.Count > 0)
                {
                    baseQuery = baseQuery.Where(e => matchedExamIds.Contains(e.Id));
                }
                else
                {
                    baseQuery = baseQuery.Where(e => e.Title.Contains(searchKeyword));
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

            var exams = await projectedQuery.ToListAsync();

            var vm = new TeacherExamListVM
            {
                Exams = exams,
                SearchKeyword = searchKeyword,
                SortBy = sortBy
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Sessions(string? searchKeyword, string statusFilter = "all", string sortBy = "start_desc", int page = 1)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var now = DateTime.Now;

            var baseQuery = _context.ExamSessions
                .AsNoTracking()
                .Where(s => s.ExamPaper.TeacherId == teacherId);

            var classrooms = await _context.Classrooms
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId && c.IsDeleted == false)
                .OrderBy(c => c.ClassName)
                .Select(c => new SessionOptionVM
                {
                    Id = c.Id,
                    Name = c.ClassName
                })
                .ToListAsync();

            var examPapers = await _context.ExamPapers
                .AsNoTracking()
                .Where(e => e.TeacherId == teacherId && e.IsDeleted == false)
                .OrderBy(e => e.Title)
                .Select(e => new SessionOptionVM
                {
                    Id = e.Id,
                    Name = e.Title,
                    DurationInMinutes = e.DurationInMinutes
                })
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var searchableSessions = await baseQuery
                    .Select(s => new ExamSessionSearchDocument
                    {
                        Id = s.Id,
                        TeacherId = s.ExamPaper.TeacherId,
                        SessionName = s.SessionName,
                        ClassName = s.Classroom.ClassName,
                        ExamTitle = s.ExamPaper.Title
                    })
                    .ToListAsync();

                await _meiliSearch.IndexTeacherExamSessionsAsync(teacherId, searchableSessions);
                var matchedSessionIds = await _meiliSearch.SearchTeacherExamSessionIdsAsync(teacherId, searchKeyword);

                if (matchedSessionIds.Count > 0)
                {
                    baseQuery = baseQuery.Where(s => matchedSessionIds.Contains(s.Id));
                }
                else
                {
                    baseQuery = baseQuery.Where(s =>
                        s.SessionName.Contains(searchKeyword) ||
                        s.Classroom.ClassName.Contains(searchKeyword) ||
                        s.ExamPaper.Title.Contains(searchKeyword));
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

            if (page < 1)
            {
                page = 1;
            }

            var totalItems = await projectedQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

            if (page > totalPages)
            {
                page = totalPages;
            }

            var sessions = await projectedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new TeacherSessionListVM
            {
                Sessions = sessions,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Classrooms = classrooms,
                ExamPapers = examPapers,
                SearchKeyword = searchKeyword,
                StatusFilter = statusFilter,
                SortBy = sortBy
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetExamPreview(int examPaperId)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
            }

            var examData = await _context.ExamPapers
                .AsNoTracking()
                .Where(e => e.Id == examPaperId && e.TeacherId == teacherId && e.IsDeleted == false)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.DurationInMinutes,
                    Questions = e.Questions
                        .OrderBy(q => q.Id)
                        .Select(q => new
                        {
                            q.Id,
                            q.Content,
                            q.Explanation,
                            q.OptionA,
                            q.OptionB,
                            q.OptionC,
                            q.OptionD,
                            q.CorrectOption
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (examData is null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy đề thi hợp lệ." });
            }

            var exam = new
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
            };

            return Json(new
            {
                success = true,
                exam
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSessionDetail(int sessionId)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
            }

            var sessionData = await _context.ExamSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId && s.Classroom.TeacherId == teacherId)
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

            if (sessionData is null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy ca thi." });
            }

            var now = DateTime.Now;
            string status = "Sắp diễn ra";
            if (now >= sessionData.StartTime && now <= sessionData.EndTime)
            {
                status = "Đang diễn ra";
            }
            else if (now > sessionData.EndTime)
            {
                status = "Đã kết thúc";
            }

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
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized();
            }

            var now = DateTime.Now;

            var sessionInfo = await _context.ExamSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId && s.Classroom.TeacherId == teacherId)
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

            if (sessionInfo is null)
            {
                return NotFound();
            }

            var studentRows = await _context.ClassroomMembers
                .AsNoTracking()
                .Where(cm => cm.ClassroomId == sessionInfo.ClassroomId)
                .Select(cm => new
                {
                    cm.StudentId,
                    cm.Student.FullName,
                    cm.Student.Email,
                    Submission = cm.Student.Submissions
                        .Where(sub => sub.ExamSessionId == sessionId)
                        .OrderByDescending(sub => sub.StartedAt)
                        .Select(sub => new
                        {
                            sub.Id,
                            sub.StartedAt,
                            sub.SubmittedAt,
                            sub.Score,
                            sub.CorrectAnswersCount,
                            sub.WarningCount,
                            sub.Status
                        })
                        .FirstOrDefault()
                })
                .OrderBy(x => x.FullName)
                .ToListAsync();

            var studentItems = studentRows
                .Select(x =>
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
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var keyword = searchKeyword.Trim();
                studentItems = studentItems
                    .Where(x => x.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || x.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            studentItems = statusFilter switch
            {
                "submitted" => studentItems.Where(x => x.SubmissionStatus == "Đã nộp bài").ToList(),
                "in_progress" => studentItems.Where(x => x.SubmissionStatus == "Đang làm bài").ToList(),
                "not_started" => studentItems.Where(x => x.SubmissionStatus == "Chưa vào thi").ToList(),
                "late" => studentItems.Where(x => x.SubmissionStatus == "Hết giờ chưa nộp").ToList(),
                _ => studentItems
            };

            var vm = new SessionMonitorPageVM
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
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(
            string? sessionName,
            int? classroomId,
            int? examPaperId,
            DateTime? startTime,
            DateTime? endTime,
            string? sessionPassword,
            bool allowViewExplanation = true,
            bool shuffleQuestions = true,
            bool shuffleAnswers = true,
            string? notes = null)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
            }

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(sessionName))
            {
                errors.Add("Trường Tên ca thi là trường bắt buộc.");
            }

            if (!classroomId.HasValue)
            {
                errors.Add("Trường Lớp học tham gia là trường bắt buộc.");
            }

            if (!examPaperId.HasValue)
            {
                errors.Add("Trường Đề thi là trường bắt buộc.");
            }

            if (!startTime.HasValue)
            {
                errors.Add("Trường Giờ bắt đầu là trường bắt buộc.");
            }

            if (!endTime.HasValue)
            {
                errors.Add("Trường Giờ kết thúc là trường bắt buộc.");
            }

            var now = DateTime.Now;

            if (startTime.HasValue && startTime.Value < now)
            {
                errors.Add("Giờ bắt đầu không được là thời gian trong quá khứ.");
            }

            if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value)
            {
                errors.Add("Giờ kết thúc phải lớn hơn giờ bắt đầu.");
            }

            if (startTime.HasValue && endTime.HasValue)
            {
                var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
                if (computedDuration <= 0)
                {
                    errors.Add("Thời lượng ca thi không hợp lệ.");
                }
            }

            var classroomExists = classroomId.HasValue && await _context.Classrooms
                .AsNoTracking()
                .AnyAsync(c => c.Id == classroomId.Value && c.TeacherId == teacherId && c.IsDeleted == false);

            if (classroomId.HasValue && !classroomExists)
            {
                errors.Add("Lớp học đã chọn không hợp lệ.");
            }

            var examPaperExists = examPaperId.HasValue && await _context.ExamPapers
                .AsNoTracking()
                .AnyAsync(e => e.Id == examPaperId.Value && e.TeacherId == teacherId && e.IsDeleted == false);

            if (examPaperId.HasValue && !examPaperExists)
            {
                errors.Add("Đề thi đã chọn không hợp lệ.");
            }

            var examPaperDuration = 0;

            if (examPaperId.HasValue)
            {
                examPaperDuration = await _context.ExamPapers
                    .AsNoTracking()
                    .Where(e => e.Id == examPaperId.Value && e.TeacherId == teacherId && e.IsDeleted == false)
                    .Select(e => e.DurationInMinutes)
                    .FirstOrDefaultAsync();
            }

            if (examPaperId.HasValue && examPaperDuration <= 0)
            {
                errors.Add("Đề thi chưa cấu hình thời gian hợp lệ.");
            }

            if (startTime.HasValue && endTime.HasValue && examPaperDuration > 0)
            {
                var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
                if (computedDuration < examPaperDuration)
                {
                    errors.Add($"Tổng thời gian ca thi phải lớn hơn hoặc bằng thời gian của đề thi ({examPaperDuration} phút).");
                }
            }

            if (classroomId.HasValue && startTime.HasValue && endTime.HasValue && classroomExists)
            {
                var conflictingSession = await _context.ExamSessions
                    .AsNoTracking()
                    .Where(s => s.ClassroomId == classroomId.Value)
                    // Overlap condition: existing.Start < new.End AND existing.End > new.Start
                    .Where(s => s.StartTime < endTime.Value && s.EndTime > startTime.Value)
                    .OrderBy(s => s.StartTime)
                    .Select(s => new { s.SessionName, s.StartTime, s.EndTime })
                    .FirstOrDefaultAsync();

                if (conflictingSession is not null)
                {
                    errors.Add($"Lớp học đã có ca thi trùng thời gian ({conflictingSession.StartTime:dd/MM/yyyy HH:mm} - {conflictingSession.EndTime:dd/MM/yyyy HH:mm}): {conflictingSession.SessionName}.");
                }
            }

            if (errors.Count > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = errors.First(),
                    errors
                });
            }

            var durationInMinutes = (int)Math.Ceiling((endTime!.Value - startTime!.Value).TotalMinutes);

            var session = new ExamSession
            {
                SessionName = sessionName!.Trim(),
                ClassroomId = classroomId!.Value,
                ExamPaperId = examPaperId!.Value,
                StartTime = startTime!.Value,
                EndTime = endTime!.Value,
                DurationInMinutes = durationInMinutes,
                SessionPassword = string.IsNullOrWhiteSpace(sessionPassword) ? null : sessionPassword.Trim(),
                AllowViewExplanation = allowViewExplanation,
                IsShuffled = shuffleQuestions || shuffleAnswers,
                ShuffleQuestions = shuffleQuestions,
                ShuffleAnswers = shuffleAnswers,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };

            _context.ExamSessions.Add(session);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Tạo ca thi thành công.",
                sessionId = session.Id
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSession(
            int? sessionId,
            string? sessionName,
            int? classroomId,
            int? examPaperId,
            DateTime? startTime,
            DateTime? endTime,
            string? sessionPassword,
            bool allowViewExplanation = true,
            bool shuffleQuestions = true,
            bool shuffleAnswers = true,
            string? notes = null)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
            }

            if (!sessionId.HasValue || sessionId.Value <= 0)
            {
                return BadRequest(new { success = false, message = "ID ca thi không hợp lệ." });
            }

            var session = await _context.ExamSessions
                .Include(s => s.Classroom)
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.Classroom.TeacherId == teacherId);

            if (session is null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy ca thi để cập nhật." });
            }

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(sessionName))
            {
                errors.Add("Trường Tên ca thi là trường bắt buộc.");
            }

            if (!examPaperId.HasValue)
            {
                errors.Add("Trường Đề thi là trường bắt buộc.");
            }

            if (!classroomId.HasValue)
            {
                errors.Add("Trường Lớp học là trường bắt buộc.");
            }

            if (!startTime.HasValue)
            {
                errors.Add("Trường Giờ bắt đầu là trường bắt buộc.");
            }

            if (!endTime.HasValue)
            {
                errors.Add("Trường Giờ kết thúc là trường bắt buộc.");
            }

            if (errors.Any())
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value)
            {
                errors.Add("Giờ kết thúc phải lớn hơn giờ bắt đầu.");
            }

            if (startTime.HasValue && endTime.HasValue)
            {
                var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
                if (computedDuration <= 0)
                {
                    errors.Add("Thời lượng ca thi không hợp lệ.");
                }
            }

            var classroomExists = classroomId.HasValue && await _context.Classrooms
                .AsNoTracking()
                .AnyAsync(c => c.Id == classroomId.Value && c.TeacherId == teacherId && c.IsDeleted == false);

            if (classroomId.HasValue && !classroomExists)
            {
                errors.Add("Lớp học đã chọn không hợp lệ.");
            }

            var examPaperExists = examPaperId.HasValue && await _context.ExamPapers
                .AsNoTracking()
                .AnyAsync(e => e.Id == examPaperId.Value && e.TeacherId == teacherId && e.IsDeleted == false);

            if (examPaperId.HasValue && !examPaperExists)
            {
                errors.Add("Đề thi đã chọn không hợp lệ.");
            }

            var examPaperDuration = 0;

            if (examPaperId.HasValue)
            {
                examPaperDuration = await _context.ExamPapers
                    .AsNoTracking()
                    .Where(e => e.Id == examPaperId.Value && e.TeacherId == teacherId && e.IsDeleted == false)
                    .Select(e => e.DurationInMinutes)
                    .FirstOrDefaultAsync();
            }

            if (examPaperId.HasValue && examPaperDuration <= 0)
            {
                errors.Add("Đề thi chưa cấu hình thời gian hợp lệ.");
            }

            if (startTime.HasValue && endTime.HasValue && examPaperDuration > 0)
            {
                var computedDuration = (int)Math.Ceiling((endTime.Value - startTime.Value).TotalMinutes);
                if (computedDuration < examPaperDuration)
                {
                    errors.Add($"Tổng thời gian ca thi phải lớn hơn hoặc bằng thời gian của đề thi ({examPaperDuration} phút).");
                }
            }

            // Check for conflicting sessions (excluding current session)
            if (classroomId.HasValue && startTime.HasValue && endTime.HasValue && classroomExists)
            {
                var conflictingSession = await _context.ExamSessions
                    .AsNoTracking()
                    .Where(s => s.ClassroomId == classroomId.Value)
                    .Where(s => s.Id != sessionId.Value)
                    .Where(s => s.StartTime < endTime.Value && s.EndTime > startTime.Value)
                    .OrderBy(s => s.StartTime)
                    .Select(s => new { s.SessionName, s.StartTime, s.EndTime })
                    .FirstOrDefaultAsync();

                if (conflictingSession is not null)
                {
                    errors.Add($"Lớp học đã có ca thi trùng thời gian ({conflictingSession.StartTime:dd/MM/yyyy HH:mm} - {conflictingSession.EndTime:dd/MM/yyyy HH:mm}): {conflictingSession.SessionName}.");
                }
            }

            if (errors.Any())
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors });
            }

            var durationInMinutes = (int)Math.Ceiling((endTime!.Value - startTime!.Value).TotalMinutes);

            session.SessionName = sessionName!.Trim();
            session.ClassroomId = classroomId!.Value;
            session.ExamPaperId = examPaperId!.Value;
            session.StartTime = startTime!.Value;
            session.EndTime = endTime!.Value;
            session.DurationInMinutes = durationInMinutes;
            session.SessionPassword = string.IsNullOrWhiteSpace(sessionPassword) ? null : sessionPassword.Trim();
            session.AllowViewExplanation = allowViewExplanation;
            session.IsShuffled = shuffleQuestions || shuffleAnswers;
            session.ShuffleQuestions = shuffleQuestions;
            session.ShuffleAnswers = shuffleAnswers;
            session.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            _context.ExamSessions.Update(session);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Cập nhật ca thi thành công.",
                sessionId = session.Id
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(int? sessionId)
        {
            var teacherIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(teacherIdStr, out int teacherId))
            {
                return Unauthorized(new { success = false, message = "Không xác định được giảng viên." });
            }

            if (!sessionId.HasValue || sessionId.Value <= 0)
            {
                return BadRequest(new { success = false, message = "ID ca thi không hợp lệ." });
            }

            var session = await _context.ExamSessions
                .Include(s => s.Classroom)
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.Classroom.TeacherId == teacherId);

            if (session is null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy ca thi để xóa." });
            }

            // Only allow deletion if session hasn't started yet (status must be "Sắp diễn ra")
            if (session.StartTime <= DateTime.Now)
            {
                return BadRequest(new { success = false, message = "Chỉ có thể xóa ca thi chưa bắt đầu." });
            }

            _context.ExamSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Xóa ca thi thành công."
            });
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
            if (submittedAt.HasValue)
            {
                return "Đã nộp bài";
            }

            if (!rawStatus.HasValue)
            {
                return now > sessionEndTime ? "Hết giờ chưa nộp" : "Chưa vào thi";
            }

            if (rawStatus.Value == 1)
            {
                return "Đã nộp bài";
            }

            if (rawStatus.Value == 0)
            {
                return now > sessionEndTime ? "Hết giờ chưa nộp" : "Đang làm bài";
            }

            return now > sessionEndTime ? "Hết giờ chưa nộp" : "Đang làm bài";
        }

        private static int? CalculateTimeSpentMinutes(DateTime? startedAt, DateTime? submittedAt, DateTime sessionEndTime, DateTime now)
        {
            if (!startedAt.HasValue)
            {
                return null;
            }

            var end = submittedAt ?? (now < sessionEndTime ? now : sessionEndTime);
            if (end <= startedAt.Value)
            {
                return 0;
            }

            return (int)Math.Ceiling((end - startedAt.Value).TotalMinutes);
        }
    }
}