using KLCN060.Api.DTOs.System;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

/// <summary>Cấu hình sao lưu định kỳ tự động (mục "Backup:Schedule" trong appsettings). Mặc định TẮT.</summary>
public class ScheduledBackupOptions
{
    public const string SectionName = "Backup:Schedule";

    /// <summary>true = API tự sao lưu khi đã đến hạn (kể cả khi không ai mở ứng dụng).</summary>
    public bool Enabled { get; set; }

    /// <summary>Chu kỳ sao lưu (giờ). Ví dụ 24 = mỗi ngày một lần.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Sau một lần tự sao lưu thất bại thì chờ bấy nhiêu phút mới thử lại (tránh thử liên tục).</summary>
    public int RetryAfterMinutes { get; set; } = 60;

    /// <summary>Bao lâu kiểm tra hạn một lần (phút).</summary>
    public int CheckEveryMinutes { get; set; } = 10;

    /// <summary>Thư mục lưu file .bak (trên máy chạy API); bỏ trống = thư mục mặc định của SQL Server.</summary>
    public string? ThuMuc { get; set; }

    /// <summary>Tên đăng nhập ghi vào lịch sử với vai trò người thực hiện; bỏ trống = tài khoản Quản lý đang hoạt động đầu tiên.</summary>
    public string? TaiKhoan { get; set; }
}

/// <summary>
/// Dịch vụ nền tự sao lưu CSDL theo chu kỳ (mục 12 của Quản lý: backup &amp; recovery). Kiểm tra định kỳ: nếu bản sao lưu thành công gần nhất đã quá chu kỳ
/// (và không vừa thử thất bại) thì gọi <see cref="IBackupService"/> như khi Quản lý bấm "Sao lưu ngay"; kết quả nằm trong lịch sử sao lưu kèm ghi chú "tự động".
/// </summary>
public class ScheduledBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<ScheduledBackupOptions> _options;
    private readonly ILogger<ScheduledBackupService> _logger;

    public ScheduledBackupService(IServiceScopeFactory scopes, IOptionsMonitor<ScheduledBackupOptions> options, ILogger<ScheduledBackupService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);   // để API khởi động xong (migrate + seed) trước
            while (!stoppingToken.IsCancellationRequested)
            {
                var opt = BackupScheduleStore.PhuLen(_options.CurrentValue);
                if (opt.Enabled)
                {
                    try
                    {
                        await RunIfDueAsync(_scopes, opt, DateTime.Now, _logger, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Sao lưu tự động gặp lỗi");
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, opt.CheckEveryMinutes)), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // dừng ứng dụng
        }
    }

    /// <summary>
    /// Đã đến hạn tự sao lưu chưa: chưa từng sao lưu thành công, hoặc bản thành công gần nhất đã quá <paramref name="chuKyGio"/> giờ; nhưng nếu vừa thử (thất bại)
    /// cách đây chưa tới <paramref name="thuLaiSauPhut"/> phút thì chờ.
    /// </summary>
    public static bool DenHan(DateTime? lanThanhCongCuoi, DateTime? lanThuCuoi, DateTime bayGio, int chuKyGio, int thuLaiSauPhut)
    {
        if (chuKyGio < 1) return false;
        if (lanThanhCongCuoi is { } tc && bayGio - tc < TimeSpan.FromHours(chuKyGio)) return false;
        if (lanThuCuoi is { } thu && bayGio - thu < TimeSpan.FromMinutes(Math.Max(0, thuLaiSauPhut))) return false;
        return true;
    }

    /// <summary>Kiểm tra hạn và (nếu đến hạn) sao lưu một lần. Trả về bản ghi lịch sử vừa tạo, hoặc null nếu chưa đến hạn / không có tài khoản để ghi lịch sử.</summary>
    public static async Task<BackupHistoryDto?> RunIfDueAsync(IServiceScopeFactory scopes, ScheduledBackupOptions opt, DateTime bayGio, ILogger logger, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KLCN060DbContext>();

        var lanSaoLuu = await context.LichSuSaoLuus.AsNoTracking().Where(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.SAO_LUU)
            .OrderByDescending(x => x.ThoiGianThucHien).Select(x => new { x.ThoiGianThucHien, x.KetQua }).ToListAsync(ct);
        var thanhCongCuoi = lanSaoLuu.FirstOrDefault(x => x.KetQua == KetQuaSaoLuu.THANH_CONG)?.ThoiGianThucHien;
        var thuCuoi = lanSaoLuu.FirstOrDefault()?.ThoiGianThucHien;
        if (!DenHan(thanhCongCuoi, thuCuoi, bayGio, opt.IntervalHours, opt.RetryAfterMinutes)) return null;

        var tenDN = !string.IsNullOrWhiteSpace(opt.TaiKhoan)
            ? opt.TaiKhoan.Trim()
            : await context.TaiKhoans.AsNoTracking()
                .Where(x => x.TrangThai == TrangThaiTaiKhoan.HOAT_DONG && x.LoaiTaiKhoan == LoaiTaiKhoan.NHAN_VIEN && x.NhanVien != null && x.NhanVien.VaiTro.TenVaiTro == "QUAN_LY")
                .OrderBy(x => x.TenDN).Select(x => x.TenDN).FirstOrDefaultAsync(ct);
        if (tenDN is null)
        {
            logger.LogWarning("Sao lưu tự động: không có tài khoản Quản lý đang hoạt động để ghi lịch sử, bỏ qua lần này.");
            return null;
        }

        var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var user = new CurrentUser(tenDN, null, null, false);
        try
        {
            var ketQua = await backup.BackupAsync(new BackupRequest { ThuMuc = string.IsNullOrWhiteSpace(opt.ThuMuc) ? null : opt.ThuMuc }, user, tuDong: true);
            logger.LogInformation("Sao lưu tự động thành công: {File}", ketQua.DuongDanFile);
            return ketQua;
        }
        catch (Middlewares.ApiException ex)
        {
            logger.LogError("Sao lưu tự động thất bại: {Message}", ex.Message);
            // Lỗi khi SQL Server ghi file: BackupService đã ghi dòng THAT_BAI. Lỗi cấu hình (VD thư mục không hợp lệ) bị chặn trước khi sao lưu nên chưa có dòng nào:
            // tự ghi một dòng THAT_BAI để Quản lý thấy trong tình trạng sao lưu và để lần kiểm tra kế tiếp chờ đủ thời gian rồi mới thử lại.
            if (ex.Code != "SAO_LUU_THAT_BAI")
            {
                context.LichSuSaoLuus.Add(new Domain.LichSuSaoLuu
                {
                    LoaiThaoTac = LoaiThaoTacSaoLuu.SAO_LUU,
                    ThoiGianThucHien = DateTime.Now,
                    MaTaiKhoan = tenDN,
                    DuongDanFile = string.IsNullOrWhiteSpace(opt.ThuMuc) ? null : opt.ThuMuc,
                    KetQua = KetQuaSaoLuu.THAT_BAI,
                    NguonKichHoat = "TU_DONG",
                    ThongBaoLoi = ex.Message is { Length: > 1000 } l ? l[..1000] : ex.Message,
                    GhiChu = ("Sao lưu tự động không chạy được: " + ex.Message) is { Length: > 255 } dai ? dai[..255] : "Sao lưu tự động không chạy được: " + ex.Message
                });
                await context.SaveChangesAsync(ct);
            }
            return null;
        }
    }
}
