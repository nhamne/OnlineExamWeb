using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels
{
    public class ExamResultVM
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; }
        public string ClassName { get; set; }
        public double? Score { get; set; }
        public int? CorrectAnswersCount { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int WarningCount { get; set; }
        public bool AllowViewScore { get; set; }
        public bool AllowViewExplanation { get; set; }
        public List<ExamResultDetailVM> Details { get; set; } = new List<ExamResultDetailVM>();
        public List<StudentScoreChartItemVM> ScoreHistory { get; set; } = new List<StudentScoreChartItemVM>();
    }

    public class ExamResultDetailVM
    {
        public int QuestionId { get; set; }
        public string Content { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; }
        public string Explanation { get; set; }
        public string SelectedOption { get; set; }
        public bool? IsCorrect { get; set; }
    }
}
