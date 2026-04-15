using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class GiaoDich
{
    public int Id { get; set; }

    public string? SoTkgui { get; set; }

    public string? SoTknhan { get; set; }

    public long SoTien { get; set; }

    public string Loai { get; set; } = null!;

    public string? MoTa { get; set; }

    public DateTime ThoiGian { get; set; }
}
