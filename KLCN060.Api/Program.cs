using System.Text;
using System.Text.Json;
using KLCN060.Api.Auditing;
using KLCN060.Api.Controllers;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services;
using KLCN060.Api.Services.Auth;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using KLCN060.Api.Services.Payments;
using KLCN060.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

Console.OutputEncoding = System.Text.Encoding.UTF8;   // log tiếng Việt không bị "?" trên cửa sổ console
var builder = WebApplication.CreateBuilder(args);
// Cấu hình riêng của máy (email gửi thư, tài khoản ngân hàng nhận tiền...): nằm trong thư mục dự án, đã .gitignore, nạp SAU CÙNG nên đè lên mọi nguồn khác.
// Test tích hợp (môi trường "IntegrationTest") KHÔNG nạp file này: kết quả test không được phụ thuộc cấu hình thật của từng máy (số tài khoản ngân hàng, SMTP...).
if (!builder.Environment.IsEnvironment("IntegrationTest"))
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Lỗi model-binding (VD: query string ?checkIn=abc không parse được DateOnly) xảy ra TRƯỚC khi vào
        // Controller/Service nên không đi qua ExceptionHandlingMiddleware — mặc định ASP.NET Core trả
        // ValidationProblemDetails (tiếng Anh, khác cấu trúc) phá vỡ hợp đồng { success:false, error:{...} }
        // ở Mục 4. Bọc lại đúng envelope chuẩn tại đây, lấy field/message lỗi đầu tiên.
        options.InvalidModelStateResponseFactory = context =>
        {
            var cacLoi = context.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => new { Key = kv.Key, Error = kv.Value!.Errors[0] })
                .ToList();

            // [Khối 7b - K7-5] Khi JSON body sai kiểu 1 trường, ModelState có 2 lỗi: "$.giaDV" (nguyên nhân thật) và
            // "request" ("The request field is required." - hệ quả do body không deserialize được). Trước đây lấy lỗi
            // đầu tiên → field = "request", message tiếng Anh, client không biết trường nào sai. Ưu tiên lỗi JSON-path.
            var loi = cacLoi.FirstOrDefault(x => x.Key.StartsWith('$')) ?? cacLoi.FirstOrDefault();
            var tenThamSo = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var field = loi is null || tenThamSo.Contains(loi.Key) ? null : ChuanHoaTenTruong(loi.Key);

            string message;
            if (loi is null)
                message = "Dữ liệu gửi lên không hợp lệ.";
            else if (loi.Key.StartsWith('$'))
                message = field is null
                    ? "Nội dung JSON sai cú pháp."
                    : $"Trường '{field}' không đúng kiểu dữ liệu hoặc định dạng.";
            else if (field is null)
                message = "Thiếu nội dung request (JSON body).";
            else if (loi.Error.ErrorMessage.EndsWith("field is required.", StringComparison.Ordinal))
                message = $"Trường '{field}' là bắt buộc.";
            else
                message = string.IsNullOrWhiteSpace(loi.Error.ErrorMessage)
                    ? loi.Error.Exception?.Message ?? "Dữ liệu gửi lên không hợp lệ."
                    : loi.Error.ErrorMessage;

            var body = ApiErrorResponse.Of("DU_LIEU_KHONG_HOP_LE", message, field);
            return new BadRequestObjectResult(body);
        };

        // [Khối 7b - K7-4] Tắt ProblemDetails mặc định cho lỗi client (415, 404 từ NotFound()...) để mọi response lỗi
        // không có body rơi xuống UseStatusCodePages bên dưới và được bọc đúng envelope Mục 4.
        options.SuppressMapClientErrors = true;
    });

// Chuẩn hóa key ModelState về đúng tên trường JSON phía client (camelCase): "$.khach[0].maPhong" → "khach[0].maPhong",
// "HoTen" → "hoTen", "CCCD" → "cccd"; "$" (lỗi cú pháp toàn body) → null.
static string? ChuanHoaTenTruong(string key)
{
    var k = key.StartsWith("$.") ? key[2..] : key == "$" ? string.Empty : key;
    if (k.Length == 0) return null;
    return string.Join('.', k.Split('.').Select(seg =>
    {
        var ngoac = seg.IndexOf('[');
        var ten = ngoac < 0 ? seg : seg[..ngoac];
        return JsonNamingPolicy.CamelCase.ConvertName(ten) + (ngoac < 0 ? string.Empty : seg[ngoac..]);
    }));
}

// Sổ nhật ký của request + bộ chặn SaveChanges: nhật ký thao tác được lưu cùng transaction với nghiệp vụ, và mọi lần đổi tình trạng phòng
// (từ bất kỳ đường nào: lễ tân, buồng phòng, quản lý, nhận/trả/đổi phòng) đều để lại một dòng lịch sử.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditTrail>();
builder.Services.AddScoped<IAuditTrail>(sp => sp.GetRequiredService<AuditTrail>());
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<KLCN060DbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql => sql.CommandTimeout(120))
        .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoomTypeService, RoomTypeService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDichVuService, DichVuService>();
builder.Services.AddScoped<IServicePackageService, ServicePackageService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
// Chatbot: bản đối sánh từ khóa luôn có sẵn; LlmChatbotService thêm câu trả lời bằng mô hình ngôn ngữ khi bật cấu hình "Chatbot:Llm" (mặc định tắt) và tự lùi về bản từ khóa khi lỗi.
builder.Services.AddMemoryCache();
builder.Services.Configure<KLCN060.Api.Services.Chatbot.LlmOptions>(builder.Configuration.GetSection(KLCN060.Api.Services.Chatbot.LlmOptions.SectionName));
builder.Services.AddSingleton<KLCN060.Api.Services.Chatbot.LlmChatbotState>();
builder.Services.AddHttpClient<KLCN060.Api.Services.Chatbot.ILlmClient, KLCN060.Api.Services.Chatbot.LlmClient>(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddScoped<ChatbotService>();
builder.Services.AddScoped<KLCN060.Api.Services.Chatbot.ChatbotContextBuilder>();
builder.Services.AddScoped<IChatbotService, KLCN060.Api.Services.Chatbot.LlmChatbotService>();
builder.Services.AddScoped<IGuestService, GuestService>();
builder.Services.AddScoped<IStayService, StayService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IFrontDeskService, FrontDeskService>();
builder.Services.AddScoped<IHousekeepingService, HousekeepingService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<ISupplyService, SupplyService>();
builder.Services.AddScoped<IRoomCheckService, RoomCheckService>();
builder.Services.AddScoped<ILostFoundService, LostFoundService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
// Hóa đơn điện tử: nhà cung cấp chọn bằng EInvoice:Provider (mặc định NONE = chưa kết nối, hóa đơn chờ phát hành).
builder.Services.Configure<KLCN060.Api.Services.EInvoice.EInvoiceOptions>(builder.Configuration.GetSection(KLCN060.Api.Services.EInvoice.EInvoiceOptions.SectionName));
builder.Services.AddHttpClient<KLCN060.Api.Services.EInvoice.MisaEInvoiceProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<KLCN060.Api.Services.EInvoice.ChuaKetNoiEInvoiceProvider>();
builder.Services.AddScoped<KLCN060.Api.Services.EInvoice.IEInvoiceProvider>(sp =>
    string.Equals(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KLCN060.Api.Services.EInvoice.EInvoiceOptions>>().Value.Provider, "MISA", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<KLCN060.Api.Services.EInvoice.MisaEInvoiceProvider>()
        : sp.GetRequiredService<KLCN060.Api.Services.EInvoice.ChuaKetNoiEInvoiceProvider>());
builder.Services.AddScoped<KLCN060.Api.Services.EInvoice.IEInvoiceService, KLCN060.Api.Services.EInvoice.EInvoiceService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<KLCN060.Api.Middlewares.KhoangNgayBaoCaoFilter>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IManagementReportService, ManagementReportService>();
builder.Services.AddScoped<KLCN060.Api.Services.Finance.IManagementReportExporter, KLCN060.Api.Services.Finance.ManagementReportExporter>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.SectionName));
// Cài đặt sao lưu tự động do Quản lý đặt trên giao diện: nạp sau cùng nên đè lên appsettings; tự nạp lại khi file đổi.
BackupScheduleStore.DamBaoThuMuc();
builder.Configuration.AddJsonFile(BackupScheduleStore.DuongDan, optional: true, reloadOnChange: true);
builder.Services.Configure<ScheduledBackupOptions>(builder.Configuration.GetSection(ScheduledBackupOptions.SectionName));
builder.Services.AddHostedService<ScheduledBackupService>();
builder.Services.Configure<KLCN060.Api.Services.Finance.FinanceOptions>(builder.Configuration.GetSection(KLCN060.Api.Services.Finance.FinanceOptions.SectionName));
builder.Services.AddScoped<KLCN060.Api.Services.Finance.ITransferReconciliationService, KLCN060.Api.Services.Finance.TransferReconciliationService>();
builder.Services.AddScoped<IRateService, RateService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IRoomImageService, RoomImageService>();

// Thanh toán chuyển khoản vào tài khoản ngân hàng thật: cấu hình mục "Payment" + service xử lý (tạo QR, webhook ngân hàng, đối soát).
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Quên / đặt lại / đổi mật khẩu + gửi email THẬT qua SMTP (MailKit). Thư đi qua hàng đợi nền có thử lại (EmailDispatcher);
// chưa cấu hình SMTP thì chức năng quên mật khẩu trả 503 chứ không giả vờ đã gửi.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<ChannelEmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<ChannelEmailQueue>());
builder.Services.AddHostedService<EmailDispatcher>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

// IP thật của người dùng: Website (máy chủ trung gian) chuyển tiếp qua header X-Forwarded-For. Chỉ tin header này khi request đến từ
// máy chủ đáng tin (loopback + Auth:TrustedProxies) - kẻ gọi API trực tiếp không giả IP được.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    foreach (var ip in builder.Configuration.GetSection($"{AuthOptions.SectionName}:TrustedProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(ip, out var diaChi))
            options.KnownProxies.Add(diaChi);
});

// Giới hạn "quên mật khẩu" theo IP (bổ sung cho giới hạn theo tài khoản trong PasswordService): chặn việc spam hộp thư của nhiều người.
// Số lượt/cửa sổ đọc từ cấu hình Auth: mỗi IP một bộ đếm.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PasswordController.RateLimitPolicy, httpContext =>
    {
        var cauHinh = httpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, cauHinh.ForgotPasswordMaxPerIp),
            Window = TimeSpan.FromMinutes(Math.Max(1, cauHinh.ForgotPasswordWindowMinutes)),
            QueueLimit = 0
        });
    });
    // Chatbot LLM: mỗi IP một bộ đếm theo phút để không ai làm cạn lượt miễn phí của mô hình; tắt LLM thì không giới hạn (chatbot từ khóa rất nhẹ).
    options.AddPolicy(ChatbotController.RateLimitPolicy, httpContext =>
    {
        var llm = httpContext.RequestServices.GetRequiredService<IOptionsMonitor<KLCN060.Api.Services.Chatbot.LlmOptions>>().CurrentValue;
        if (!llm.Enabled) return RateLimitPartition.GetNoLimiter("chatbot-khong-gioi-han");
        return RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, llm.RatePerMinutePerIp),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    options.OnRejected = async (rejected, cancellationToken) =>
    {
        var response = rejected.HttpContext.Response;
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        response.ContentType = "application/json";
        await response.WriteAsync(JsonSerializer.Serialize(
            ApiErrorResponse.Of("QUA_NHIEU_YEU_CAU", "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút."),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
    };
});

// Cho phép JS chạy trên trình duyệt (Website, ví dụ chatbot widget) gọi thẳng API bằng fetch() -
// chỉ MVC Controller-to-server (ApiClient của Web/Desktop) không bị CORS chặn, nhưng client-side JS thì có.
// Danh sách origin đọc từ cấu hình (Cors:AllowedOrigins), không hard-code cổng của Web.
const string CorsPolicyName = "WebClient";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key. Thiết lập qua User Secrets: dotnet user-secrets set \"Jwt:Key\" \"...\"");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
// Phân quyền theo QUYỀN lưu trong CSDL (VaiTro_Quyen, chỉnh ở D15): [HasPermission(...)] được đánh giá lại ở mỗi request nên có hiệu lực ngay.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, KLCN060.Api.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, KLCN060.Api.Authorization.PermissionHandler>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
// Đảm bảo 401/403 do [Authorize] từ chối cũng trả đúng envelope { success:false, error:{...} } (Mục 4) -
// mặc định ASP.NET Core ghi thẳng status code với body rỗng, phá vỡ hợp đồng JSON mà Web/Desktop phụ thuộc.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "KLCN060 Hotel Management API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập access token dạng: Bearer {token}"
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Tự động áp dụng migration mới nhất và seed dữ liệu bắt buộc (Mục 6) mỗi khi ứng dụng khởi động.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<KLCN060DbContext>();
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Phải đứng đầu pipeline để mọi middleware phía sau (giới hạn tần suất, nhật ký) thấy IP thật của người dùng.
app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// [Khối 7b - K7-4] Các lỗi do chính framework sinh ra KHÔNG có body (404 route không tồn tại / vi phạm route constraint,
// 405 sai phương thức, 415 sai Content-Type...) trước đây trả body rỗng hoặc application/problem+json — vi phạm hợp đồng
// Mục 4 giống hệt vấn đề đã xử lý cho 401/403 ở ApiAuthorizationMiddlewareResultHandler. Chỉ tác động response lỗi
// CHƯA có body; response đã có envelope (ApiException, 401/403, model validation) giữ nguyên.
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var (code, message) = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ("KHONG_TIM_THAY_DUONG_DAN", "Không tìm thấy endpoint được yêu cầu."),
        StatusCodes.Status405MethodNotAllowed => ("PHUONG_THUC_KHONG_HO_TRO", "Endpoint không hỗ trợ phương thức HTTP này."),
        StatusCodes.Status415UnsupportedMediaType => ("KIEU_NOI_DUNG_KHONG_HO_TRO", "Chỉ chấp nhận nội dung JSON (Content-Type: application/json)."),
        _ => ($"LOI_HTTP_{response.StatusCode}", "Yêu cầu không thể xử lý.")
    };
    response.ContentType = "application/json";
    await response.WriteAsync(JsonSerializer.Serialize(ApiErrorResponse.Of(code, message), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
});

app.UseHttpsRedirection();

// Phục vụ ảnh phòng do quản lý tải lên (Desktop D11) tại /media/rooms/{tenFile}. Thư mục lưu nằm ngoài wwwroot,
// cấu hình được qua Media:RoomImagesPath. no-cache = trình duyệt luôn hỏi lại (ETag) nên thay ảnh là thấy ngay.
var thuMucAnhPhong = RoomImageStorage.ResolveRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(thuMucAnhPhong);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(thuMucAnhPhong),
    RequestPath = RoomImageStorage.RequestPath,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

app.UseCors(CorsPolicyName);

app.UseRateLimiter();

// Phai dat truoc UseAuthentication de "boc" toan bo pipeline: den luot doc context.User o chieu ve,
// UseAuthentication/UseAuthorization ben trong da chay xong nen claims va status code cuoi cung deu san sang.
app.UseMiddleware<AuditLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Chế độ thử nghiệm thanh toán cho khách tự "xác nhận đã chuyển khoản": TUYỆT ĐỐI không được bật ngoài môi trường phát triển.
if (!app.Environment.IsDevelopment() && app.Services.GetRequiredService<IOptions<PaymentOptions>>().Value.DemoMode && !app.Environment.IsEnvironment("IntegrationTest"))
    throw new InvalidOperationException("Payment:DemoMode đang bật ngoài môi trường Development: khách có thể tự xác nhận đã chuyển khoản mà không trả tiền. Hãy đặt Payment:DemoMode=false.");

// Ghi rõ lúc khởi động hai cấu hình hay thiếu (email, ngân hàng) để biết ngay API đang chạy có đọc được User Secrets hay chưa.
{
    var po = app.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;
    var eo = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
    var soTk = po.Bank.AccountNumber ?? "";
    app.Logger.LogInformation("File cấu hình riêng appsettings.Local.json: {TrangThai}", File.Exists(Path.Combine(app.Environment.ContentRootPath, "appsettings.Local.json")) ? "đã nạp" : "KHÔNG thấy trong " + app.Environment.ContentRootPath);
    app.Logger.LogInformation("Cấu hình ngân hàng: {TrangThai}", po.BankDaCauHinh
        ? $"tài khoản THẬT {po.Bank.BankName} ****{(soTk.Length >= 4 ? soTk[^4..] : soTk)} ({po.Bank.AccountName})"
        : po.DemoMode ? "CHƯA đủ thông tin (cần BankName, BankBin, AccountNumber, AccountName) - đang dùng tài khoản MẪU (chế độ thử nghiệm)" : "CHƯA cấu hình - không tạo được mã QR");
    app.Logger.LogInformation("Cấu hình email: {TrangThai}", !string.IsNullOrWhiteSpace(eo.Smtp.Host) && !string.IsNullOrWhiteSpace(eo.Smtp.From)
        ? $"{eo.Smtp.Host}:{eo.Smtp.Port} (gửi từ {eo.Smtp.From})" : "CHƯA cấu hình (Email:Smtp:Host/From) - Quên mật khẩu sẽ báo lỗi");
}

app.Run();

// [Khoi 7 - Integration Testing, 24/09/2026] Chuong trinh top-level statement mac dinh sinh ra 1 class
// "Program" ẩn (implicit/internal), WebApplicationFactory<Program> (dung trong KLCN060.Api.IntegrationTests)
// can 1 kieu PUBLIC de tham chieu duoc tu assembly test. Khai bao "partial" nay KHONG doi hanh vi runtime -
// no chi lam cho class Program do trinh bien dich tu sinh tro thanh public thay vi internal.
public partial class Program { }
