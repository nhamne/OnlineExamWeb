USE OnlineExamDB;
GO

DECLARE @TargetClassId INT;
SELECT TOP 1 @TargetClassId = Id FROM Classrooms WHERE JoinCode = 'DOTNET25';

IF @TargetClassId IS NOT NULL
BEGIN
    DECLARE @TargetPaperId INT;
    SELECT TOP 1 @TargetPaperId = ExamPaperId FROM ExamSessions WHERE ClassroomId = @TargetClassId;

    DELETE FROM SubmissionDetails WHERE SubmissionId IN (SELECT Id FROM Submissions WHERE ExamSessionId IN (SELECT Id FROM ExamSessions WHERE ClassroomId = @TargetClassId));
    DELETE FROM Submissions WHERE ExamSessionId IN (SELECT Id FROM ExamSessions WHERE ClassroomId = @TargetClassId);
    DELETE FROM ExamSessions WHERE ClassroomId = @TargetClassId;
    
    IF @TargetPaperId IS NOT NULL
    BEGIN
        DELETE FROM Questions WHERE ExamPaperId = @TargetPaperId;
        DELETE FROM ExamPapers WHERE Id = @TargetPaperId;
    END

    DELETE FROM ClassroomMembers WHERE ClassroomId = @TargetClassId;
    DELETE FROM Classrooms WHERE Id = @TargetClassId;
END
GO

INSERT INTO Classrooms (ClassName, JoinCode, TeacherId, IsDeleted) 
VALUES (N'Thực hành ASP.NET Core', 'DOTNET25', 1, 0);

DECLARE @NewClassId INT = SCOPE_IDENTITY();

INSERT INTO ExamPapers (Title, TeacherId, IsDeleted) 
VALUES (N'Bài kiểm tra thực hành Web', 1, 0);

DECLARE @NewPaperId INT = SCOPE_IDENTITY();

INSERT INTO Questions (ExamPaperId, Content, OptionA, OptionB, OptionC, OptionD, CorrectOption, Explanation) VALUES
(@NewPaperId, N'Đâu là framework web của .NET?', N'Laravel', N'Spring Boot', N'ASP.NET Core', N'Django', 'C', N'ASP.NET Core là web framework của Microsoft.'),
(@NewPaperId, N'Kỹ thuật nào giúp chống tấn công CSRF trong ASP.NET Core?', N'CORS', N'Anti-forgery Tokens', N'SQL Injection', N'XSS', 'B', N'Sử dụng Anti-forgery tokens (ValidateAntiForgeryToken) để chống CSRF.'),
(@NewPaperId, N'DbContext thuộc thư viện nào?', N'Entity Framework Core', N'Dapper', N'ADO.NET', N'NHibernate', 'A', N'DbContext là thành phần cốt lõi của EF Core.');

INSERT INTO ExamSessions (SessionName, ClassroomId, ExamPaperId, StartTime, EndTime, DurationInMinutes, SessionPassword, AllowViewScore, IsShuffled) VALUES
(N'Bài thi thử ASP.NET Core 2025', @NewClassId, @NewPaperId, DATEADD(hour, -2, GETDATE()), DATEADD(day, 2, GETDATE()), 30, NULL, 1, 1);
GO
