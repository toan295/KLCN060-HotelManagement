using System.Text.Json;
using KLCN060.Api.DTOs.System;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class BackupService
{
    private static readonly object _khoaNhatKy = new();

    // ===== Nhật ký sao lưu NGOÀI CSDL =====
    // Lịch sử sao lưu nằm trong chính CSDL nên sẽ bị "lùi" khi phục hồi. Mỗi thao tác được ghi thêm ra một file nối đuôi (JSON Lines) nằm ngoài CSDL;
    // sau phục hồi, các dòng bị mất được nạp lại để vẫn truy vết được "ai đã làm gì, lúc nào" kể cả với chính lần phục hồi.
    private string DuongDanNhatKyNgoai()
    {
        var cauHinh = _configuration["Backup:JournalPath"];
        return !string.IsNullOrWhiteSpace(cauHinh) ? cauHinh : Path.Combine(AppContext.BaseDirectory, "App_Data", "backup-journal.jsonl");
    }

    private sealed record DongNhatKy(DateTime ThoiGian, string Loai, string TaiKhoan, string? Tep, string KetQua, string? GhiChu, string? TenCSDL,
        long? KichThuoc, int? ThoiLuongMs, string? Nguon, string? LyDo, int? MaNguon, bool? DaKiemTra, string? Loi);

    private void GhiNhatKyNgoai(LichSuSaoLuu x)
    {
        try
        {
            var duongDan = DuongDanNhatKyNgoai();
            Directory.CreateDirectory(Path.GetDirectoryName(duongDan)!);
            var dong = new DongNhatKy(x.ThoiGianThucHien, x.LoaiThaoTac.ToString(), x.MaTaiKhoan, x.DuongDanFile, x.KetQua.ToString(), x.GhiChu, x.TenCSDL,
                x.KichThuocBytes, x.ThoiLuongMs, x.NguonKichHoat, x.LyDo, x.MaBanNguon, x.DaKiemTraHopLe, x.ThongBaoLoi);
            lock (_khoaNhatKy) File.AppendAllText(duongDan, JsonSerializer.Serialize(dong) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký sao lưu ngoài CSDL");
        }
    }

    /// <summary>Nạp lại vào CSDL (vừa phục hồi) các dòng lịch sử có trong nhật ký ngoài nhưng đã bị mất do CSDL lùi về quá khứ. Trả số dòng đã nạp.</summary>
    private async Task<int> NapLaiLichSuTuNhatKyNgoaiAsync(CurrentUser user)
    {
        try
        {
            var duongDan = DuongDanNhatKyNgoai();
            if (!File.Exists(duongDan)) return 0;
            string[] dong;
            lock (_khoaNhatKy) dong = File.ReadAllLines(duongDan);

            var tkCoSan = (await _context.TaiKhoans.AsNoTracking().Select(x => x.TenDN).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var daCo = (await _context.LichSuSaoLuus.AsNoTracking().Select(x => new { x.ThoiGianThucHien, x.LoaiThaoTac, x.MaTaiKhoan }).ToListAsync())
                .Select(x => (x.ThoiGianThucHien.Ticks / TimeSpan.TicksPerSecond, x.LoaiThaoTac.ToString(), x.MaTaiKhoan)).ToHashSet();
            var nap = 0;
            foreach (var d in dong)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                DongNhatKy? r;
                try { r = JsonSerializer.Deserialize<DongNhatKy>(d); } catch (JsonException) { continue; }
                if (r is null || !tkCoSan.Contains(r.TaiKhoan)) continue;
                if (!Enum.TryParse<LoaiThaoTacSaoLuu>(r.Loai, out var loai) || !Enum.TryParse<KetQuaSaoLuu>(r.KetQua, out var kq)) continue;
                if (daCo.Contains((r.ThoiGian.Ticks / TimeSpan.TicksPerSecond, r.Loai, r.TaiKhoan))) continue;
                _context.LichSuSaoLuus.Add(new LichSuSaoLuu
                {
                    LoaiThaoTac = loai, ThoiGianThucHien = r.ThoiGian, MaTaiKhoan = r.TaiKhoan, DuongDanFile = r.Tep, KetQua = kq, GhiChu = r.GhiChu,
                    TenCSDL = r.TenCSDL, KichThuocBytes = r.KichThuoc, ThoiLuongMs = r.ThoiLuongMs, NguonKichHoat = r.Nguon, LyDo = r.LyDo,
                    MaBanNguon = null, DaKiemTraHopLe = r.DaKiemTra, ThongBaoLoi = r.Loi
                });
                nap++;
            }
            if (nap > 0) await _context.SaveChangesAsync();
            return nap;
        }
        catch (Exception ex) when (ex is IOException or DbUpdateException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Không nạp lại được lịch sử sao lưu từ nhật ký ngoài");
            _context.ChangeTracker.Clear();
            return 0;
        }
    }

    // ===== Kiểm tra sau phục hồi =====
    private async Task<(bool DatYeuCau, string MoTa)> KiemTraSauPhucHoiAsync(SqlConnectionStringBuilder builder, string tenDb)
    {
        var ds = new List<string>();
        var dat = true;
        try
        {
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            int Dem(string sql) { using var c = conn.CreateCommand(); c.CommandText = sql; return Convert.ToInt32(c.ExecuteScalar()); }
            string Bang<T>() => "[" + (_context.Model.FindEntityType(typeof(T))!.GetTableName()) + "]";
            ds.Add($"CSDL mở được, có {Dem("SELECT COUNT(*) FROM sys.tables")} bảng, {Dem("SELECT COUNT(*) FROM " + Bang<TaiKhoan>())} tài khoản, {Dem("SELECT COUNT(*) FROM " + Bang<PhieuDatPhong>())} phiếu đặt phòng, {Dem("SELECT COUNT(*) FROM " + Bang<HoaDon>())} hóa đơn");

            // DBCC CHECKDB mức vật lý: phát hiện hỏng trang dữ liệu sau khi ghi đè (nhanh với CSDL cỡ khách sạn).
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 120;
            cmd.CommandText = $"DBCC CHECKDB ({QuoteName(tenDb)}) WITH NO_INFOMSGS, PHYSICAL_ONLY";
            try { await cmd.ExecuteNonQueryAsync(); ds.Add("DBCC CHECKDB (mức vật lý): không có lỗi"); }
            catch (SqlException ex) { dat = false; ds.Add($"DBCC CHECKDB báo lỗi: {ex.Message}"); }

            // Cấu trúc sau phục hồi phải khớp mô hình của ứng dụng (Database First: cấu trúc do script Database/*.sql quy định):
            // bản sao lưu từ phiên bản cấu trúc cũ thiếu bảng thì báo rõ để chạy script nâng cấp.
            var coTrongCsdl = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var dsBang = conn.CreateCommand())
            {
                dsBang.CommandText = "SELECT name FROM sys.tables";
                await using var rd = await dsBang.ExecuteReaderAsync();
                while (await rd.ReadAsync()) coTrongCsdl.Add(rd.GetString(0));
            }
            var thieu = _context.Model.GetEntityTypes().Select(x => x.GetTableName()).Where(x => x is not null).Distinct().Where(x => !coTrongCsdl.Contains(x!)).ToList();
            ds.Add(thieu.Count == 0 ? "Cấu trúc CSDL khớp phiên bản ứng dụng" : $"LƯU Ý: bản sao lưu thiếu {thieu.Count} bảng so với ứng dụng hiện tại ({string.Join(", ", thieu.Take(5))}) - cần chạy script cập nhật cấu trúc");
        }
        catch (SqlException ex)
        {
            dat = false;
            ds.Add($"Không mở được CSDL sau phục hồi: {ex.Message}");
        }

        return (dat, string.Join("; ", ds) + ".");
    }

    // ===== Xem trước phục hồi =====
    public async Task<RestorePreviewDto> PreviewRestoreAsync(int? maLichSu, RestoreFileRequest? file, CurrentUser user)
    {
        var tenDb = LayConnectionStringBuilder().InitialCatalog;
        string duongDan;
        if (maLichSu is { } ma) duongDan = (await LayBanSaoLuuPhucHoiAsync(ma)).DuongDanFile!;
        else duongDan = ChuanHoaFileSaoLuu(file ?? new RestoreFileRequest());

        var kt = await KiemTraFileAsync(duongDan, user, "Xem trước phục hồi", ghiLichSu: false);
        var dto = new RestorePreviewDto { TenCSDL = tenDb, DuongDanFile = duongDan, KiemTra = kt, ThoiDiemBanSaoLuu = kt.ThoiDiemSaoLuuTrongFile };
        if (!kt.HopLe) { dto.KetLuan = $"KHÔNG thể phục hồi: {kt.ThongBao}"; dto.CanhBao.Add(kt.ThongBao); return dto; }
        if (kt.DungCSDL == false) { dto.KetLuan = $"KHÔNG thể phục hồi: {kt.ThongBao}"; dto.CanhBao.Add(kt.ThongBao); return dto; }

        dto.SanSang = true;
        if (kt.ThoiDiemSaoLuuTrongFile is { } moc)
        {
            dto.SoThaoTacSeMat = await _context.NhatKyThaoTacs.AsNoTracking().CountAsync(x => x.ThoiGian > moc);
            dto.SoDatPhongMoiSeMat = await _context.PhieuDatPhongs.AsNoTracking().CountAsync(x => x.NgayDat > moc);
            dto.SoHoaDonMoiSeMat = await _context.HoaDons.AsNoTracking().CountAsync(x => x.NgayLap > moc);
            var gio = (int)Math.Floor((DateTime.Now - moc).TotalHours);
            dto.CanhBao.Add($"Mọi dữ liệu phát sinh SAU {moc:dd/MM/yyyy HH:mm} (cách đây {gio} giờ) sẽ bị thay thế: {dto.SoThaoTacSeMat} thao tác nghiệp vụ, {dto.SoDatPhongMoiSeMat} phiếu đặt phòng mới, {dto.SoHoaDonMoiSeMat} hóa đơn.");
        }
        dto.CanhBao.Add("Tất cả người dùng đang đăng nhập sẽ bị ngắt kết nối trong lúc phục hồi.");
        dto.CanhBao.Add("Hệ thống sẽ tự chụp một bản sao lưu an toàn của dữ liệu hiện tại ngay trước khi ghi đè, để có thể quay lại.");
        dto.KetLuan = "File hợp lệ và đúng CSDL - có thể phục hồi sau khi bạn nhập lý do và gõ lại tên CSDL để xác nhận.";
        return dto;
    }
}
