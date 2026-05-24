using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OnlineExam.Models;
using OnlineExam.ViewModels;
using System.Security.Claims;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace OnlineExam.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly OnlineExamDbContext _context;

        public StudentController(OnlineExamDbContext context)
        {
            _context = context;
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
                    Score = (double)s.Score,
                    SubmittedAt = s.SubmittedAt ?? DateTime.Now
                })
                .ToList();

            var vm = new StudentDashboardVM
            {
                StudentName = student.FullName,
                PendingExamsCount = pendingExamsCount,
                Exams = examItems.OrderByDescending(e => e.StartTime).ToList(),
                JoinedClasses = joinedClasses,
                ScoreHistory = scoreHistory
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult JoinClass()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");
            return View();
        }

        [HttpGet]
        public IActionResult MyExams()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            AutoSubmitExpiredExams(student);

            var now = DateTime.Now;

            var joinedClassIds = _context.ClassroomMembers
                .Where(cm => cm.StudentId == student.Id)
                .Select(cm => cm.ClassroomId)
                .ToList();

            var joinedClasses = _context.Classrooms
                .Where(c => joinedClassIds.Contains(c.Id) && c.IsDeleted != true)
                .Select(c => new ClassroomVM { Id = c.Id, ClassName = c.ClassName })
                .ToList();

            var sessions = _context.ExamSessions
                .Include(s => s.Classroom)
                .ThenInclude(c => c.Teacher)
                .Include(s => s.ExamPaper)
                .Where(s => joinedClassIds.Contains(s.ClassroomId))
                .ToList();

            var submissions = _context.Submissions
                .Where(s => s.StudentId == student.Id)
                .ToList();

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

            var vm = new StudentDashboardVM
            {
                StudentName = student.FullName,
                Exams = examItems.OrderByDescending(e => e.StartTime).ToList(),
                JoinedClasses = joinedClasses
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Results()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (student == null) return RedirectToAction("Login", "Auth");

            AutoSubmitExpiredExams(student);

            var submittedExams = _context.Submissions
                .Include(s => s.ExamSession)
                .Include(s => s.ExamSession.Classroom)
                .Where(s => s.StudentId == student.Id && (s.Status == 1 || s.Status == 2))
                .OrderByDescending(s => s.SubmittedAt)
                .ToList();

            var scoreHistory = submittedExams
                .Where(s => s.Score != null)
                .OrderBy(s => s.SubmittedAt)
                .Select(s => new StudentScoreChartItemVM
                {
                    ExamName = s.ExamSession.SessionName,
                    Score = (double)s.Score,
                    SubmittedAt = s.SubmittedAt ?? DateTime.Now
                })
                .ToList();

            var vm = new StudentResultsVM
            {
                StudentName = student.FullName,
                Submissions = submittedExams,
                ScoreHistory = scoreHistory
            };

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
                    Score = (double)s.Score,
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
                        CorrectOption = q.CorrectOption.ToString(),
                        Explanation = q.Explanation,
                        SelectedOption = ans?.SelectedOption,
                        IsCorrect = ans?.IsCorrect
                    });
                }
                vm.Details = detailsList;
            }

            return View(vm);
        }
    }
}