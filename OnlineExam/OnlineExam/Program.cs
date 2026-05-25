using Microsoft.AspNetCore.Authentication.Cookies;
using OnlineExam.Models;
using OnlineExam.Services.Search;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<OnlineExamDbContext>();
builder.Services.AddHttpClient<IMeiliSearchService, MeiliSearchService>();

builder.Services.AddSession(); // Đăng ký dịch vụ thẻ nhớ tạm

// 1. Khai báo dịch vụ Auth (Thêm trước app.Build)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Nếu chưa login mà đòi vào, đá về trang này
        options.AccessDeniedPath = "/Auth/Login"; // Cấm quyền cũng đá về đây
        options.ExpireTimeSpan = TimeSpan.FromDays(1); // Cho phép lưu đăng nhập 1 ngày
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
// ... sau dòng app.UseStaticFiles();
app.UseSession(); // Cho phép web sử dụng thẻ nhớ này

app.UseRouting();

// 2. Bật Auth (BẮT BUỘC phải đặt trước Authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
