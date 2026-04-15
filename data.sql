USE OnlineExamDB;
GO

-- 1. Insert Users (2 Teachers, 5 Students)
IF NOT EXISTS (SELECT 1 FROM Users)
BEGIN
    INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive) VALUES
    (N'Nguyễn Văn Giảng Viên', 'teacher1@edu.vn', 'password@123', 'Teacher', 1),
    (N'Trần Thị Cô Giáo', 'teacher2@edu.vn', 'password@123', 'Teacher', 1),
    (N'Hoàng Nam Khánh', 'student1@edu.vn', 'password@123', 'Student', 1),
    (N'Nguyễn Thị Thùy Nhâm', 'student2@edu.vn', 'password@123', 'Student', 1),
    (N'Phạm Ngọc Nhi', 'student3@edu.vn', 'password@123', 'Student', 1),
    (N'Lê Minh Cường', 'student4@edu.vn', 'password@123', 'Student', 1),
    (N'Đỗ An Bình', 'student5@edu.vn', 'password@123', 'Student', 1);
END
GO

-- 2. Insert Classrooms
IF NOT EXISTS (SELECT 1 FROM Classrooms)
BEGIN
    INSERT INTO Classrooms (ClassName, JoinCode, TeacherId, IsDeleted) VALUES
    (N'Lập trình Web Nâng Cao - D18CNPM1', 'WEBNC1', 1, 0),
    (N'Cơ sở Dữ liệu - D18CNPM2', 'CSDL02', 2, 0);
END
GO

-- 3. Insert ClassroomMembers (Gán học sinh vào lớp)
IF NOT EXISTS (SELECT 1 FROM ClassroomMembers)
BEGIN
    -- Lớp Web Nâng Cao có 3 sinh viên đầu
    INSERT INTO ClassroomMembers (ClassroomId, StudentId) VALUES
    (1, 3), (1, 4), (1, 5),
    -- Lớp CSDL có 4 sinh viên
    (2, 4), (2, 5), (2, 6), (2, 7);
END
GO

-- 4. Insert ExamPapers (2 Đề thi)
IF NOT EXISTS (SELECT 1 FROM ExamPapers)
BEGIN
    INSERT INTO ExamPapers (Title, TeacherId, IsDeleted) VALUES
    (N'Bài kiểm tra ASP.NET MVC Giữa kỳ', 1, 0),
    (N'Trắc nghiệm SQL Server Cơ bản', 2, 0);
END
GO

-- 5. Insert Questions (5 câu cho đề 1, 5 câu cho đề 2)
IF NOT EXISTS (SELECT 1 FROM Questions)
BEGIN
    -- Đề 1 (ASP.NET MVC) - ExamPaperId = 1
    INSERT INTO Questions (ExamPaperId, Content, OptionA, OptionB, OptionC, OptionD, CorrectOption, Explanation) VALUES
    (1, N'Mô hình MVC là viết tắt của từ gì?', N'Model View Control', N'Model View Controller', N'Module View Controller', N'Main View Controller', 'B', N'MVC là viết tắt của Model-View-Controller.'),
    (1, N'Trong ASP.NET MVC, file nào dùng để cấu hình routing mặc định?', N'RouteConfig.cs', N'Startup.cs', N'Global.asax', N'Web.config', 'A', N'RouteConfig.cs chứa cấu hình các route của ứng dụng.'),
    (1, N'Thuộc tính nào dùng để đánh dấu một Action chỉ nhận request dạng POST?', N'[HttpGet]', N'[AcceptVerbs(HttpVerbs.Get)]', N'[HttpPost]', N'[ActionName]', 'C', N'[HttpPost] bắt buộc request phải là POST.'),
    (1, N'Từ khóa nào dùng để truyền dữ liệu từ Controller sang View mà chỉ tồn tại trong 1 request?', N'ViewBag', N'Session', N'TempData', N'Application', 'A', N'ViewBag và ViewData có vòng đời trong 1 request.'),
    (1, N'Entity Framework thuộc loại công nghệ nào?', N'ORM', N'MVC', N'API', N'SPA', 'A', N'EF là một Object-Relational Mapper (ORM).');

    -- Đề 2 (SQL Server) - ExamPaperId = 2
    INSERT INTO Questions (ExamPaperId, Content, OptionA, OptionB, OptionC, OptionD, CorrectOption, Explanation) VALUES
    (2, N'Lệnh nào dùng để xóa toàn bộ dữ liệu trong bảng mà không ghi log chi tiết?', N'DROP', N'DELETE', N'TRUNCATE', N'REMOVE', 'C', N'TRUNCATE xóa nhanh và không ghi log chi tiết từng row.'),
    (2, N'Khóa ngoại (Foreign Key) có tác dụng gì?', N'Tăng tốc độ tìm kiếm', N'Đảm bảo tính toàn vẹn tham chiếu', N'Mã hóa dữ liệu', N'Tự động tăng giá trị', 'B', N'FK ràng buộc dữ liệu giữa 2 bảng phải hợp lệ.'),
    (2, N'Để lọc kết quả sau khi đã GROUP BY, ta dùng mệnh đề nào?', N'WHERE', N'HAVING', N'ORDER BY', N'FILTER', 'B', N'HAVING được dùng để điều kiện hóa các nhóm dữ liệu.'),
    (2, N'Kiểu dữ liệu nào lưu trữ chuỗi có hỗ trợ Unicode trong SQL Server?', N'VARCHAR', N'TEXT', N'CHAR', N'NVARCHAR', 'D', N'Chữ N (National) đại diện cho chuẩn Unicode.'),
    (2, N'Lệnh JOIN nào lấy tất cả bản ghi ở bảng bên trái, dù không khớp với bảng bên phải?', N'INNER JOIN', N'RIGHT JOIN', N'LEFT JOIN', N'FULL JOIN', 'C', N'LEFT JOIN bảo toàn dữ liệu bảng bên trái.');
END
GO

-- 6. Insert ExamSessions (Ca thi)
IF NOT EXISTS (SELECT 1 FROM ExamSessions)
BEGIN
    INSERT INTO ExamSessions (SessionName, ClassroomId, ExamPaperId, StartTime, EndTime, DurationInMinutes, SessionPassword, AllowViewScore, IsShuffled) VALUES
    (N'Ca thi sáng - Lập trình Web', 1, 1, DATEADD(hour, -1, GETDATE()), DATEADD(hour, 2, GETDATE()), 45, '123456', 1, 1),
    (N'Kiểm tra 15 phút CSDL', 2, 2, DATEADD(day, 1, GETDATE()), DATEADD(day, 1, DATEADD(hour, 1, GETDATE())), 15, NULL, 0, 1);
END
GO

-- 7. Insert Submissions (Sinh viên 3, 4 đã thi xong đề 1)
IF NOT EXISTS (SELECT 1 FROM Submissions)
BEGIN
    INSERT INTO Submissions (ExamSessionId, StudentId, StartedAt, SubmittedAt, Status, Score, CorrectAnswersCount, WarningCount) VALUES
    (1, 3, DATEADD(minute, -40, GETDATE()), DATEADD(minute, -10, GETDATE()), 1, 8.0, 4, 0),
    (1, 4, DATEADD(minute, -35, GETDATE()), DATEADD(minute, -5, GETDATE()), 1, 10.0, 5, 1);
END
GO

-- 8. Insert SubmissionDetails (Chi tiết bài làm của sinh viên 3 và 4)
IF NOT EXISTS (SELECT 1 FROM SubmissionDetails)
BEGIN
    -- Chi tiết bài làm của sinh viên 3 (SubmissionId = 1) - Đúng 4/5 câu
    INSERT INTO SubmissionDetails (SubmissionId, QuestionId, SelectedOption, IsCorrect) VALUES
    (1, 1, 'B', 1), -- Đúng
    (1, 2, 'A', 1), -- Đúng
    (1, 3, 'C', 1), -- Đúng
    (1, 4, 'B', 0), -- Sai (Chọn Session thay vì ViewBag)
    (1, 5, 'A', 1); -- Đúng

    -- Chi tiết bài làm của sinh viên 4 (SubmissionId = 2) - Đúng 5/5 câu
    INSERT INTO SubmissionDetails (SubmissionId, QuestionId, SelectedOption, IsCorrect) VALUES
    (2, 1, 'B', 1),
    (2, 2, 'A', 1),
    (2, 3, 'C', 1),
    (2, 4, 'A', 1),
    (2, 5, 'A', 1);
END
GO