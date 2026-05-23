USE OnlineExamDB;
GO

DECLARE @StudentId INT = (SELECT Id FROM Users WHERE Email = 'student3@edu.vn');
DECLARE @TeacherId INT = 1;

-- 1. Create 2 Classrooms
INSERT INTO Classrooms (ClassName, JoinCode, TeacherId, IsDeleted) VALUES 
(N'Kiến trúc máy tính', 'KTMT2025', @TeacherId, 0),
(N'Hệ điều hành', 'OS2025', @TeacherId, 0);

DECLARE @ClassKTMT INT = (SELECT Id FROM Classrooms WHERE JoinCode = 'KTMT2025');
DECLARE @ClassOS INT = (SELECT Id FROM Classrooms WHERE JoinCode = 'OS2025');

-- 2. Add Student to Classrooms
INSERT INTO ClassroomMembers (ClassroomId, StudentId, JoinedAt) VALUES 
(@ClassKTMT, @StudentId, GETDATE()),
(@ClassOS, @StudentId, GETDATE());

-- 3. Create Exam Papers
INSERT INTO ExamPapers (Title, TeacherId, IsDeleted) VALUES 
(N'Bài kiểm tra số 1 - KTMT', @TeacherId, 0),
(N'Bài kiểm tra giữa kỳ - KTMT', @TeacherId, 0),
(N'Quiz 1 - Hệ điều hành', @TeacherId, 0),
(N'Bài tập lớn - Hệ điều hành', @TeacherId, 0),
(N'Thi cuối kỳ - Hệ điều hành', @TeacherId, 0);

DECLARE @Paper1 INT = (SELECT Id FROM ExamPapers WHERE Title = N'Bài kiểm tra số 1 - KTMT');
DECLARE @Paper2 INT = (SELECT Id FROM ExamPapers WHERE Title = N'Bài kiểm tra giữa kỳ - KTMT');
DECLARE @Paper3 INT = (SELECT Id FROM ExamPapers WHERE Title = N'Quiz 1 - Hệ điều hành');
DECLARE @Paper4 INT = (SELECT Id FROM ExamPapers WHERE Title = N'Bài tập lớn - Hệ điều hành');
DECLARE @Paper5 INT = (SELECT Id FROM ExamPapers WHERE Title = N'Thi cuối kỳ - Hệ điều hành');

-- 4. Add Dummy Questions
INSERT INTO Questions (ExamPaperId, Content, OptionA, OptionB, OptionC, OptionD, CorrectOption, Explanation) VALUES
(@Paper1, N'Câu 1', N'A', N'B', N'C', N'D', 'A', ''),
(@Paper2, N'Câu 1', N'A', N'B', N'C', N'D', 'B', ''),
(@Paper3, N'Câu 1', N'A', N'B', N'C', N'D', 'C', ''),
(@Paper4, N'Câu 1', N'A', N'B', N'C', N'D', 'D', ''),
(@Paper5, N'Câu 1', N'A', N'B', N'C', N'D', 'A', '');

-- 5. Create Exam Sessions
INSERT INTO ExamSessions (SessionName, ClassroomId, ExamPaperId, StartTime, EndTime, DurationInMinutes, AllowViewScore, IsShuffled) VALUES
(N'Kiểm tra KTMT số 1 (Đã nộp)', @ClassKTMT, @Paper1, DATEADD(day, -5, GETDATE()), DATEADD(day, 5, GETDATE()), 15, 1, 0),
(N'Kiểm tra giữa kỳ KTMT (Chưa làm)', @ClassKTMT, @Paper2, DATEADD(hour, -2, GETDATE()), DATEADD(day, 2, GETDATE()), 60, 1, 1),
(N'Quiz 1 OS (Sắp hết hạn)', @ClassOS, @Paper3, DATEADD(day, -2, GETDATE()), DATEADD(hour, 5, GETDATE()), 10, 1, 0),
(N'BTL OS (Đã đóng)', @ClassOS, @Paper4, DATEADD(day, -10, GETDATE()), DATEADD(day, -5, GETDATE()), 120, 1, 0),
(N'Thi cuối kỳ OS (Chưa mở)', @ClassOS, @Paper5, DATEADD(day, 5, GETDATE()), DATEADD(day, 6, GETDATE()), 90, 0, 1);

-- 6. Add Submission for Exam 1
DECLARE @Session1 INT = (SELECT Id FROM ExamSessions WHERE SessionName = N'Kiểm tra KTMT số 1 (Đã nộp)');
INSERT INTO Submissions (ExamSessionId, StudentId, StartedAt, SubmittedAt, Status, Score, CorrectAnswersCount, WarningCount) VALUES
(@Session1, @StudentId, DATEADD(day, -2, GETDATE()), DATEADD(minute, 10, DATEADD(day, -2, GETDATE())), 1, 9.5, 1, 0);

GO
