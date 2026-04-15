using System;
using System.Collections.Generic;

namespace OnlineExam.Models;

public partial class TaiKhoan
{
    public string SoTk { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public long SoDu { get; set; }
}
