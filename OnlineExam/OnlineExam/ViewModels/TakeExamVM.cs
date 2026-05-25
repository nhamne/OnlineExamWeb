using System;
using System.Collections.Generic;

namespace OnlineExam.ViewModels
{
    public class TakeExamVM
    {
        public int SessionId { get; set; }
        public string SessionName { get; set; }
        public string ClassName { get; set; }
        public int DurationInMinutes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StudentStartTime { get; set; }
        public bool IsShuffled { get; set; }
        public List<ExamQuestionVM> Questions { get; set; } = new List<ExamQuestionVM>();
    }
              
    public class ExamQuestionVM
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        // Không gửi CorrectOption và Explanation về client
    }

    public class SubmitExamDTO
    {
        public int SessionId { get; set; }
        public List<AnswerDTO> Answers { get; set; } = new List<AnswerDTO>();
        public bool IsForceSubmit { get; set; }
        public int WarningCount { get; set; }
    }

    public class AnswerDTO
    {
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; }
    }
}
