-- 1. Tạo Database nếu chưa tồn tại

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'OnlineExamDBWeb')
BEGIN
    CREATE DATABASE OnlineExamDBWeb;
END
GO

USE OnlineExamDBWeb;
GO

-- 2. Bảng Users (Tài khoản)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(255) NOT NULL,
        Email VARCHAR(255) NOT NULL UNIQUE,
        PasswordHash VARCHAR(255) NOT NULL,
        Role VARCHAR(50) NOT NULL CHECK (Role IN ('Teacher', 'Student')),
        IsActive BIT DEFAULT 1
    );
END
GO

-- 3. Bảng Classrooms (Lớp học)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Classrooms' AND xtype='U')
BEGIN
    CREATE TABLE Classrooms (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClassName NVARCHAR(255) NOT NULL,
        JoinCode VARCHAR(20) NOT NULL UNIQUE,
        TeacherId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
        CreatedAt DATETIME DEFAULT GETDATE(),
        IsDeleted BIT DEFAULT 0
    );
END
GO

-- 4. Bảng ClassroomMembers (Thành viên lớp học)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ClassroomMembers' AND xtype='U')
BEGIN
    CREATE TABLE ClassroomMembers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClassroomId INT NOT NULL FOREIGN KEY REFERENCES Classrooms(Id),
        StudentId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
        JoinedAt DATETIME DEFAULT GETDATE()
    );
END
GO

-- 5. Bảng ExamPapers (Kho đề thi)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ExamPapers' AND xtype='U')
BEGIN
    CREATE TABLE ExamPapers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(255) NOT NULL,
        DurationInMinutes INT NOT NULL DEFAULT 45,
        Subject NVARCHAR(100) NULL,
        TeacherId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
        CreatedAt DATETIME DEFAULT GETDATE(),
        IsDeleted BIT DEFAULT 0,
        Status NVARCHAR(50) DEFAULT N'Bản nháp'
    );
END
GO

IF COL_LENGTH('ExamPapers', 'DurationInMinutes') IS NULL
BEGIN
    ALTER TABLE ExamPapers ADD DurationInMinutes INT NOT NULL CONSTRAINT DF_ExamPapers_DurationInMinutes DEFAULT(45);
END
GO

-- 6. Bảng Questions (Câu hỏi trắc nghiệm)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Questions' AND xtype='U')
BEGIN
    CREATE TABLE Questions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ExamPaperId INT NOT NULL FOREIGN KEY REFERENCES ExamPapers(Id),
        Content NVARCHAR(MAX) NOT NULL,
        OptionA NVARCHAR(MAX) NOT NULL,
        OptionB NVARCHAR(MAX) NOT NULL,
        OptionC NVARCHAR(MAX) NOT NULL,
        OptionD NVARCHAR(MAX) NOT NULL,
        CorrectOption CHAR(1) NOT NULL CHECK (CorrectOption IN ('A', 'B', 'C', 'D')),
        Explanation NVARCHAR(MAX) NULL
    );
END
GO

-- 7. Bảng ExamSessions (Ca thi)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ExamSessions' AND xtype='U')
BEGIN
    CREATE TABLE ExamSessions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SessionName NVARCHAR(255) NOT NULL,
        ClassroomId INT NOT NULL FOREIGN KEY REFERENCES Classrooms(Id),
        ExamPaperId INT NOT NULL FOREIGN KEY REFERENCES ExamPapers(Id),
        StartTime DATETIME NOT NULL,
        EndTime DATETIME NOT NULL,
        DurationInMinutes INT NOT NULL,
        SessionPassword VARCHAR(50) NULL,
        AllowViewExplanation BIT DEFAULT 1,
        IsShuffled BIT DEFAULT 1,
        ShuffleQuestions BIT DEFAULT 1,
        ShuffleAnswers BIT DEFAULT 1,
        Notes NVARCHAR(MAX) NULL
    );
END
GO

IF COL_LENGTH('ExamSessions', 'AllowViewExplanation') IS NULL
BEGIN
    ALTER TABLE ExamSessions ADD AllowViewExplanation BIT NOT NULL CONSTRAINT DF_ExamSessions_AllowViewExplanation DEFAULT(1);
END
GO
-- Ensure AllowViewScore column exists (nullable with default)
IF COL_LENGTH('ExamSessions', 'AllowViewScore') IS NULL
BEGIN
    ALTER TABLE ExamSessions ADD AllowViewScore BIT NULL CONSTRAINT DF_ExamSessions_AllowViewScore DEFAULT(1);
END
GO

-- If AllowViewExplanation exists and AllowViewScore is missing/NULL, copy values
IF COL_LENGTH('ExamSessions', 'AllowViewExplanation') IS NOT NULL AND COL_LENGTH('ExamSessions', 'AllowViewScore') IS NOT NULL
BEGIN
    UPDATE ExamSessions SET AllowViewScore = AllowViewExplanation WHERE AllowViewScore IS NULL;
END
GO
GO

IF COL_LENGTH('ExamSessions', 'ShuffleQuestions') IS NULL
BEGIN
    ALTER TABLE ExamSessions ADD ShuffleQuestions BIT NOT NULL CONSTRAINT DF_ExamSessions_ShuffleQuestions DEFAULT(1);
END
GO

IF COL_LENGTH('ExamSessions', 'ShuffleAnswers') IS NULL
BEGIN
    ALTER TABLE ExamSessions ADD ShuffleAnswers BIT NOT NULL CONSTRAINT DF_ExamSessions_ShuffleAnswers DEFAULT(1);
END
GO

IF COL_LENGTH('ExamSessions', 'Notes') IS NULL
BEGIN
    ALTER TABLE ExamSessions ADD Notes NVARCHAR(MAX) NULL;
END
GO

-- 8. Bảng Submissions (Bài nộp)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Submissions' AND xtype='U')
BEGIN
    CREATE TABLE Submissions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ExamSessionId INT NOT NULL FOREIGN KEY REFERENCES ExamSessions(Id),
        StudentId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
        StartedAt DATETIME NOT NULL,
        SubmittedAt DATETIME NULL,
        Status INT DEFAULT 0, -- 0: InProgress, 1: Submitted, 2: ForceSubmitted
        Score FLOAT NULL,
        CorrectAnswersCount INT NULL,
        WarningCount INT DEFAULT 0
    );
END
GO

-- 9. Bảng SubmissionDetails (Chi tiết bài làm)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SubmissionDetails' AND xtype='U')
BEGIN
    CREATE TABLE SubmissionDetails (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SubmissionId INT NOT NULL FOREIGN KEY REFERENCES Submissions(Id),
        QuestionId INT NOT NULL FOREIGN KEY REFERENCES Questions(Id),
        SelectedOption CHAR(1) NULL CHECK (SelectedOption IN ('A', 'B', 'C', 'D') OR SelectedOption IS NULL),
        IsCorrect BIT NULL
    );
END
GO