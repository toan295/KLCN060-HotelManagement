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

// Danh sách ảnh phòng (do quản lý tải lên qua Desktop) để partial _Photo biết vị trí nào đã có ảnh.
builder.Services.AddScoped<IRoomImageCatalog, RoomImageCatalog>();

// Không lộ tên máy chủ (Kestrel) trong header Server.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var app = builder.Build();

// Header bảo mật cho mọi phản hồi: chống nhúng trang vào khung của trang khác (clickjacking), đoán sai kiểu nội dung,
// lộ địa chỉ trang qua Referer, và giới hạn nguồn script/ảnh/kết nối (CSP). Nguồn bên ngoài duy nhất: Google Fonts + API của chính dự án (chatbot).
var apiOrigin = new Uri(app.Configuration["Api:BaseUrl"] ?? "http://localhost").GetLeftPart(UriPartial.Authority);
var csp = string.Join("; ",
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
    "font-src 'self' https://fonts.gstatic.com data:",
    "img-src 'self' data:",
    $"connect-src 'self' {apiOrigin}",
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'");
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = csp;
    headers["X-Frame-Options"] = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    // Trang đặt lại mật khẩu tự đặt "no-referrer" riêng; các trang còn lại chỉ gửi gốc trang khi sang site khác.
    if (!headers.ContainsKey("Referrer-Policy"))
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Mọi phản hồi lỗi không có nội dung (404, 400 do biểu mẫu hết hạn, 503...) hiện trang lỗi tiếng Việt có menu thay vì trang trắng.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

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
