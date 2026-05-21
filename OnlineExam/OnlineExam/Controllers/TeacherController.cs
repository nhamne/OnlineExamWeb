using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OnlineExam.ViewModels;
using System;
using System.Collections.Generic;using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using OnlineExam.Models;

namespace OnlineExam.Controllers // Chú ý: Đổi tên namespace cho khớp với project của bạn
{
    [Authorize(Roles = "Teacher")]
    public class TeacherController : Controller
    {
        private readonly OnlineExamDbContext _context;

        public TeacherController(OnlineExamDbContext context)
        {
            _context = context;
        }

        // Hàm này sẽ bắt đường dẫn /Teacher/Index
        public IActionResult Index()
        {
            // Dữ liệu mẫu truyền sang view để khắc phục lỗi Null Reference
            var dashboardData = new TeacherDashboardVM()
            {
                TeacherName = "Tên Giảng Viên",
                TotalClasses = 10,
                TotalExams = 8,
                OngoingSessions = 2,
                UpcomingSessions = 4,
                RecentSessions = new List<SessionItemVM>
                {
                    new SessionItemVM
                    {
                        SessionName = "Kiểm tra 15p",
                        ClassName = "Lớp 10A1",
                        StartTime = DateTime.Now.AddHours(-1),
                        EndTime = DateTime.Now.AddHours(1)
                    }
                }
            };

            // Trả về phần ruột kèm theo dữ liệu Model
            return View(dashboardData);
        }

        // Thêm đoạn này vào bên trong class TeacherController
        public async Task<IActionResult> Exams(string search = null, string subject = null, string title = null, string status = null, int page = 1)
        {
            int pageSize = 10;
            var baseQuery = _context.ExamPapers
                                .Where(e => e.IsDeleted != true)
                                .Include(e => e.Teacher)
                                .Include(e => e.ExamSessions)
                                .ThenInclude(es => es.Classroom)
                                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                baseQuery = baseQuery.Where(e => e.Title.Contains(search) || (e.Subject != null && e.Subject.Contains(search)));
            }

            if (!string.IsNullOrEmpty(subject))
            {
                baseQuery = baseQuery.Where(e => e.Subject == subject);
            }

            if (!string.IsNullOrEmpty(title))
            {
                baseQuery = baseQuery.Where(e => e.Title == title);
            }

            if (!string.IsNullOrEmpty(status))
            {
                baseQuery = baseQuery.Where(e => e.Status == status);
            }

            // 1. Tổng số đề thi
            int totalExams = await baseQuery.CountAsync();
            ViewBag.TotalExams = totalExams;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalExams / (double)pageSize);

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSubject = subject;
            ViewBag.CurrentTitle = title;
            ViewBag.CurrentStatus = status;

            var exams = await baseQuery.OrderByDescending(e => e.CreatedAt)
                                   .Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            // 2. Môn thi phổ biến nhất (dựa trên số lượng đề thi)
            var popularSubject = await _context.ExamPapers
                                            .Where(e => e.IsDeleted != true && !string.IsNullOrEmpty(e.Subject))
                                            .GroupBy(e => e.Subject)
                                            .OrderByDescending(g => g.Count())
                                            .Select(g => g.Key)
                                            .FirstOrDefaultAsync();
            ViewBag.PopularSubject = popularSubject ?? "Chưa có môn nào";

            // 3. Lấy dữ liệu thật cho mục cuối cùng (thay "AI Usage" thành "Tổng câu hỏi")
            var totalQuestions = await _context.Questions.Where(q => q.ExamPaper.IsDeleted != true).CountAsync();
            ViewBag.TotalQuestions = totalQuestions;

            // 4. Lấy dữ liệu danh sách Môn và Tên đề thi cho Bộ lọc
            ViewBag.AllSubjects = await _context.ExamPapers.Where(e => e.IsDeleted != true && !string.IsNullOrEmpty(e.Subject)).Select(e => e.Subject).Distinct().ToListAsync();
            ViewBag.AllExamTitles = await _context.ExamPapers.Where(e => e.IsDeleted != true).Select(e => e.Title).Distinct().ToListAsync();

            return View(exams);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExam(int id, string format)
        {
            var exam = await _context.ExamPapers
                .Include(e => e.Questions)
                .Include(e => e.Teacher)
                .FirstOrDefaultAsync(e => e.Id == id);
            
            if (exam == null) return NotFound(new { success = false, message = "Không tìm thấy đề thi" });

            ViewBag.Format = format;

            if (format?.ToLower() == "word")
            {
                // Force download stream for word format
                var cd = new System.Net.Mime.ContentDisposition
                {
                    FileName = $"DeThi_{id}.doc",
                    Inline = false
                };
                Response.Headers.Append("Content-Disposition", cd.ToString());
                return View("ExportDocument", exam); // This view renders basic HTML, Word will interpret it as Doc
            }
            else if (format?.ToLower() == "pdf")
            {
                // Render as raw HTML, client-side script in ExportDocument.cshtml will grab it, turn to PDF and download.
                 return View("ExportDocument", exam);
            }

            return BadRequest("Không hỗ trợ định dạng này.");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteExams([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một đề thi để xóa!" });
            }

            try
            {
                var deleteExams = await _context.ExamPapers.Where(e => ids.Contains(e.Id)).ToListAsync();
                if (deleteExams.Any())
                {
                    // Soft delete
                    foreach (var exam in deleteExams)
                    {
                        exam.IsDeleted = true;
                    }
                    await _context.SaveChangesAsync();
                }

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
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một đề thi để sao chép!" });
            }

            try
            {
                var examsToCopy = await _context.ExamPapers
                    .Include(e => e.Questions)
                    .Where(e => ids.Contains(e.Id))
                    .ToListAsync();
                
                if (examsToCopy.Any())
                {
                    foreach (var oldExam in examsToCopy)
                    {
                        var newExam = new ExamPaper
                        {
                            Title = oldExam.Title + " (Bản sao)",
                            Subject = oldExam.Subject,
                            TeacherId = oldExam.TeacherId,
                            CreatedAt = DateTime.Now,
                            IsDeleted = false,
                            Status = "Bản nháp"
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
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        // Đường dẫn sẽ là: /Teacher/CreateExam
        public IActionResult CreateExam()
        {
            return View(); // Nó sẽ tự tìm file CreateExam.cshtml trong Views/Teacher
        }
        public IActionResult OcrScanner()
        {
            return View();
        }

        public async Task<IActionResult> EditExam(int id)
        {
            var exam = await _context.ExamPapers
                .Include(e => e.Questions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                return NotFound("Không tìm thấy đề thi.");
            }

            return View(exam);
        }

        public async Task<IActionResult> ExamDetails(int id)
        {
            var exam = await _context.ExamPapers
                .Include(e => e.Questions)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                return NotFound("Không tìm thấy đề thi.");
            }

            return View(exam);
        }

        [HttpPost]
        public async Task<IActionResult> AnalyzeOCR(IFormFile file, [FromServices] IConfiguration config)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Vui lòng chọn file" });

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                string base64String = Convert.ToBase64String(fileBytes);

                // API Key láº¥y tá»« appsettings.json - hĂ£y Ä‘áº£m báº£o báº¡n Ä‘Ă£ thĂªm "GeminiApiKey": "key_cua_ban" vĂ o Ä‘Ă³
                string apiKey = config["GeminiApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                    return Json(new { success = false, message = "Chưa cấu hình GeminiApiKey trong appsettings.json. Vui lòng thêm key của bạn vào." });

                string mimeType = file.ContentType;
                // Gemini hĂµ trá»£ application/pdf, image/jpeg, image/png, image/webp
                if (mimeType != "application/pdf" && !mimeType.StartsWith("image/"))
                    return Json(new { success = false, message = "Gemini API chỉ hỗ trợ file PDF và Ảnh (JPG, PNG, WEBP)" });

                using var client = new HttpClient();
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                // XĂ¢y dá»±ng payload cho Gemini Vision
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = "Bạn là trợ lý trích xuất câu hỏi trắc nghiệm. Nhiệm vụ của bạn là lấy TOÀN BỘ câu hỏi trắc nghiệm trong tài liệu này (bỏ qua những phần không phải là câu hỏi trắc nghiệm) và định dạng chúng thành một mảng JSON HỢP LỆ. Mỗi câu hỏi trong mảng JSON phải đúng cấu trúc object sau: {\"question\": \"nội dung câu hỏi\", \"options\": [\"đáp án A\", \"đáp án B\", \"đáp án C\", \"đáp án D\"], \"correct\": \"A\" (lấy ký tự đúng nếu biết, nếu không thể đoán thì để chuỗi rỗng), \"difficulty\": \"Dễ\"}. KHÔNG được trả thêm markdown (ví dụ như ```json). TRẢ VỀ DUY NHẤT MẢNG JSON. Đảm bảo parse đầy đủ dấu, tiếng Việt chuẩn." },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = mimeType,
                                        data = base64String
                                    }
                                }
                            }
                        }
                    }
                };

                var response = await client.PostAsJsonAsync(url, payload);
                var rawResult = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Lỗi gọi AI: " + rawResult });
                }

                // Parse káº¿t quáº£ tráº£ vá» tá»« Gemini JSON
                // Cáº¥u trĂºc response cá»§a Gemini API: {"candidates": [ {"content": {"parts": [{"text": "[...]"}]}}]}
                using JsonDocument doc = JsonDocument.Parse(rawResult);
                var root = doc.RootElement;
                var candidates = root.GetProperty("candidates");
                var firstCandidate = candidates[0];
                var content = firstCandidate.GetProperty("content");
                var parts = content.GetProperty("parts");
                string jsonText = parts[0].GetProperty("text").GetString() ?? "[]";

                // Dá»n dáº¹p markdown \`\`\`json trÆ°á»›c khi parsing
                jsonText = jsonText.Replace("```json", "").Replace("```", "").Trim();

                // Chuyá»ƒn text thĂ nh Object Ä‘á»ƒ tráº£ vá» Client (trĂ¡nh client pháº£i JSON.parse vĂ²ng lĂ¡nh)
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
                // Lấy UserId từ cookie (hoặc mặc định là 1 nếu lỗi)
                int teacherId = 1; 
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int uid)) { teacherId = uid; }

                var paper = new ExamPaper
                {
                    Title = request.Title,
                    Subject = request.Subject,
                    Status = request.Status,
                    Duration = request.Duration,
                    TeacherId = teacherId,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                foreach(var q in request.Questions)
                {
                    var dbQ = new Question
                    {
                        Content = q.Text,
                        OptionA = q.Options.FirstOrDefault(o => o.Label == "A")?.Text ?? "A",
                        OptionB = q.Options.FirstOrDefault(o => o.Label == "B")?.Text ?? "B",
                        OptionC = q.Options.FirstOrDefault(o => o.Label == "C")?.Text ?? "C",
                        OptionD = q.Options.FirstOrDefault(o => o.Label == "D")?.Text ?? "D",
                        CorrectOption = q.Options.FirstOrDefault(o => o.IsCorrect)?.Label ?? "A"
                    };
                    paper.Questions.Add(dbQ);
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

                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int uid) && exam.TeacherId != uid) 
                    return Forbid();

                exam.Title = request.Title;
                exam.Subject = request.Subject;
                exam.Status = request.Status;
                exam.Duration = request.Duration;
                exam.CreatedAt = DateTime.Now; // Cập nhật lại thời gian khi chỉnh sửa đề thi

                // Xóa tất cả các bài nộp thuộc về các câu hỏi trong đề thi này để tránh lỗi khóa ngoại (Foreign key constraint)
                var questionIds = exam.Questions.Select(q => q.Id).ToList();
                var submissionDetailsToDelete = _context.Set<SubmissionDetail>().Where(s => questionIds.Contains(s.QuestionId));
                _context.Set<SubmissionDetail>().RemoveRange(submissionDetailsToDelete);

                _context.Questions.RemoveRange(exam.Questions);
                exam.Questions.Clear();

                foreach (var q in request.Questions)
                {
                    var dbQ = new Question
                    {
                        Content = q.Text,
                        OptionA = q.Options.FirstOrDefault(o => o.Label == "A")?.Text ?? "A",
                        OptionB = q.Options.FirstOrDefault(o => o.Label == "B")?.Text ?? "B",
                        OptionC = q.Options.FirstOrDefault(o => o.Label == "C")?.Text ?? "C",
                        OptionD = q.Options.FirstOrDefault(o => o.Label == "D")?.Text ?? "D",
                        CorrectOption = q.Options.FirstOrDefault(o => o.IsCorrect)?.Label ?? "A"
                    };
                    exam.Questions.Add(dbQ);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, id = exam.Id, redirectUrl = "/Teacher/Exams" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class SaveExamRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = "Bản nháp";
        public int? Duration { get; set; } = null;
        public List<QuestionRequest> Questions { get; set; } = new List<QuestionRequest>();
    }

    public class QuestionRequest
    {
        public string Number { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<OptionRequest> Options { get; set; } = new List<OptionRequest>();
    }

    public class OptionRequest
    {
        public string Label { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }    }
}