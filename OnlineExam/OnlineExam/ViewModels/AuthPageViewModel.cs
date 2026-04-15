namespace OnlineExam.ViewModels
{
    public class AuthPageViewModel
    {
        public string Role { get; set; } = "student";
        public bool IsRegister { get; set; }
        public string PageTitle { get; set; } = string.Empty;
        public string HeroTagline { get; set; } = string.Empty;
        public string HeroTitle { get; set; } = string.Empty;
        public string HeroDescription { get; set; } = string.Empty;
        public string FormTitle { get; set; } = string.Empty;
        public string FormDescription { get; set; } = string.Empty;
        public string SubmitLabel { get; set; } = string.Empty;
        public string AlternatePrompt { get; set; } = string.Empty;
        public string AlternateActionLabel { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public bool IsTeacherRole => string.Equals(Role, "teacher", StringComparison.OrdinalIgnoreCase);
        public string AlternateAction => IsRegister ? "Login" : "Register";
    }
}
