using System.Collections.Generic;

namespace OnlineExam.ViewModels;

public class ClassroomListPageVM
{
    public List<ClassroomVM> Classes { get; set; } = new();

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 6;

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public string? SearchKeyword { get; set; }

    public string StudentFilter { get; set; } = "all";

    public string SortBy { get; set; } = "created_desc";

    public bool ShowMeiliSearchWarning { get; set; }
}
