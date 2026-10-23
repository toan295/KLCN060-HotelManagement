using System.Text.Json;
using System.Text.Json.Nodes;

namespace KLCN060.Api.Services;

/// <summary>
/// Cài đặt sao lưu tự động do Quản lý bật / tắt ngay trên giao diện (không phải sửa appsettings). Lưu trong file App_Data/backup-schedule.json cạnh API;
/// file này được nạp như một nguồn cấu hình (reloadOnChange) nên đè lên mục "Backup:Schedule" trong appsettings và dịch vụ sao lưu nền đọc được ngay.
/// </summary>
public static class BackupScheduleStore
{
    public static string DuongDan => Path.Combine(AppContext.BaseDirectory, "App_Data", "backup-schedule.json");

    /// <summary>Tạo thư mục chứa file (nguồn cấu hình cần thư mục tồn tại) - gọi lúc khởi động.</summary>
    public static void DamBaoThuMuc() => Directory.CreateDirectory(Path.GetDirectoryName(DuongDan)!);

    /// <summary>Cài đặt đã lưu bằng giao diện (null nếu chưa ai đặt).</summary>
    public static (bool Bat, int ChuKyGio)? Doc()
    {
        try
        {
            if (!File.Exists(DuongDan)) return null;
            var node = JsonNode.Parse(File.ReadAllText(DuongDan));
            var s = node?["Backup"]?["Schedule"];
            if (s is null) return null;
            return (s["Enabled"]?.GetValue<bool>() ?? false, s["IntervalHours"]?.GetValue<int>() ?? 24);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    /// <summary>Lúc Quản lý lưu cài đặt lần cuối (null nếu chưa lưu): để không báo "quá hạn" ngay khi vừa bật cho một CSDL lâu chưa sao lưu.</summary>
    public static DateTime? ThoiDiemCaiDat() => File.Exists(DuongDan) ? File.GetLastWriteTime(DuongDan) : null;

    private static readonly object KhoaGhi = new();

    public static void Ghi(bool bat, int chuKyGio)
    {
        DamBaoThuMuc();
        var noiDung = JsonSerializer.Serialize(new { Backup = new { Schedule = new { Enabled = bat, IntervalHours = chuKyGio } } }, new JsonSerializerOptions { WriteIndented = true });
        // Nhiều yêu cầu lưu cùng lúc không được dùng chung một tệp tạm (trước đây tranh nhau → 500): khóa + tên tạm riêng mỗi lần ghi.
        lock (KhoaGhi)
        {
            var tam = DuongDan + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tam, noiDung);
            // Trình theo dõi cấu hình (reloadOnChange) hoặc luồng đọc khác có thể đang giữ tệp đích trong chốc lát: thử lại vài lần.
            for (var lan = 1; ; lan++)
            {
                try { File.Move(tam, DuongDan, overwrite: true); break; }
                catch (Exception ex) when (lan < 8 && ex is IOException or UnauthorizedAccessException) { Thread.Sleep(25 * lan); }
            }
        }
    }

    /// <summary>Đè cài đặt đã lưu bằng giao diện lên cấu hình lấy từ appsettings (đọc trực tiếp file để có hiệu lực tức thì, không chờ nạp lại cấu hình).</summary>
    public static ScheduledBackupOptions PhuLen(ScheduledBackupOptions goc)
    {
        if (Doc() is { } d) { goc.Enabled = d.Bat; goc.IntervalHours = d.ChuKyGio; }
        return goc;
    }
}
