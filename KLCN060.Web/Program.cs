using KLCN060.Web.Filters;
using KLCN060.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Bắt ApiSessionExpiredException từ ApiClient và chuyển hướng về W5 (Đăng nhập) - áp dụng cho mọi Controller.
    options.Filters.Add<SessionExpiredFilter>();
});

// Session dùng để lưu JWT phía server (ASP.NET Core MVC không có localStorage của trình duyệt) - Mục 3.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// ApiClient: HttpClient duy nhất gọi KLCN060.Api, BaseAddress đọc từ appsettings.json (Api:BaseUrl) - Mục 3.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl trong appsettings.json.");
    client.BaseAddress = new Uri(baseUrl);
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

app.UseRouting();

// UseSession phải đặt trước UseAuthorization (Mục 3).
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
