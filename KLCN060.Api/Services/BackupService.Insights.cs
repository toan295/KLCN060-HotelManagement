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
        }
        catch (SqlException ex)
        {
            dat = false;
            ds.Add($"Không mở được CSDL sau phục hồi: {ex.Message}");
        }

        try
        {
            var choMigration = (await _context.Database.GetPendingMigrationsAsync()).ToList();
            if (choMigration.Count > 0)
            {
                ds.Add($"LƯU Ý: bản sao lưu cũ hơn cấu trúc hiện tại, còn {choMigration.Count} migration chưa áp dụng (khởi động lại API để tự nâng cấp)");
            }
            else ds.Add("Cấu trúc CSDL khớp phiên bản ứng dụng");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            ds.Add("Không đọc được danh sách migration");
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

    // ===== Bật / tắt sao lưu tự động ngay trên giao diện =====
    public Task<BackupScheduleDto> GetScheduleAsync()
    {
        var lt = BackupScheduleStore.PhuLen(_configuration.GetSection(ScheduledBackupOptions.SectionName).Get<ScheduledBackupOptions>() ?? new ScheduledBackupOptions());
        return Task.FromResult(new BackupScheduleDto { TuDongBat = lt.Enabled, ChuKyGio = Math.Max(1, lt.IntervalHours), KiemTraMoiPhut = Math.Max(1, lt.CheckEveryMinutes), ThuMuc = string.IsNullOrWhiteSpace(lt.ThuMuc) ? null : lt.ThuMuc });
    }

    public async Task<BackupScheduleDto> SetScheduleAsync(BackupScheduleRequest request, CurrentUser user)
    {
        if (request.ChuKyGio is < 1 or > 720)
            throw new ApiException(StatusCodes.Status400BadRequest, "CHU_KY_KHONG_HOP_LE", "Chu kỳ sao lưu từ 1 đến 720 giờ.", "chuKyGio");
        var cu = await GetScheduleAsync();
        try { BackupScheduleStore.Ghi(request.TuDongBat, request.ChuKyGio); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ApiException(StatusCodes.Status500InternalServerError, "KHONG_GHI_DUOC_CAI_DAT", $"Không lưu được cài đặt sao lưu tự động trên máy chạy API: {ex.Message}");
        }
        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = user.TenDN, HanhDong = "CAU_HINH_SAO_LUU_TU_DONG", DoiTuongTacDong = "backup-schedule", ThoiGian = DateTime.Now, KetQua = "THANH_CONG",
            ChiTiet = $"truoc=[tuDong={(cu.TuDongBat ? "Có" : "Không")}; chuKyGio={cu.ChuKyGio}]; sau=[tuDong={(request.TuDongBat ? "Có" : "Không")}; chuKyGio={request.ChuKyGio}]"
        });
        await _context.SaveChangesAsync();
        return await GetScheduleAsync();
    }

    // ===== Tình trạng an toàn dữ liệu + đánh giá khả năng phục hồi =====
    public async Task<BackupStatusDto> GetStatusAsync()
    {
        var bayGio = DateTime.Now;
        var builder = LayConnectionStringBuilder();
        var lichTrinh = BackupScheduleStore.PhuLen(_configuration.GetSection(ScheduledBackupOptions.SectionName).Get<ScheduledBackupOptions>() ?? new ScheduledBackupOptions());
        var lichSu = await _context.LichSuSaoLuus.AsNoTracking().OrderByDescending(x => x.ThoiGianThucHien).ThenByDescending(x => x.MaLichSu).ToListAsync();
        var saoLuu = lichSu.Where(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.SAO_LUU).ToList();
        var thanhCong = saoLuu.Where(x => x.KetQua == KetQuaSaoLuu.THANH_CONG).ToList();
        var kiemTraCuoi = lichSu.FirstOrDefault(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.KIEM_TRA);
        var cuoi = thanhCong.FirstOrDefault();
        var soNgay = cuoi is null ? (int?)null : (int)Math.Floor((bayGio - cuoi.ThoiGianThucHien).TotalDays);
        var lanCuoi = saoLuu.FirstOrDefault();
        var lanCuoiThatBai = lanCuoi?.KetQua == KetQuaSaoLuu.THAT_BAI;
        var canhBao = new List<string>();

        string tinhTrang, tomTat;
        if (cuoi is null)
        {
            tinhTrang = "CHUA_SAO_LUU";
            tomTat = "Chưa có bản sao lưu thành công nào - hãy sao lưu ngay.";
        }
        else if (lanCuoiThatBai || soNgay > 7)
        {
            tinhTrang = "CANH_BAO";
            tomTat = lanCuoiThatBai ? "Lần sao lưu gần nhất thất bại - hãy thử lại." : $"Đã {soNgay} ngày chưa sao lưu.";
        }
        else
        {
            tinhTrang = "TOT";
            tomTat = soNgay == 0 ? "Đã sao lưu hôm nay." : $"Sao lưu gần nhất cách đây {soNgay} ngày.";
        }

        var hanTuDong = lichTrinh.Enabled && cuoi is not null ? cuoi.ThoiGianThucHien.AddHours(lichTrinh.IntervalHours) : (DateTime?)null;
        var tuDong = saoLuu.Where(x => x.NguonKichHoat == "TU_DONG" || (x.GhiChu != null && x.GhiChu.StartsWith("Sao lưu tự động"))).ToList();
        var tuDongQuaHan = lichTrinh.Enabled && hanTuDong is { } h && bayGio > (BackupScheduleStore.ThoiDiemCaiDat() is { } moc && moc > h ? moc : h).AddMinutes(Math.Max(1, lichTrinh.CheckEveryMinutes) + lichTrinh.RetryAfterMinutes);
        if (!lichTrinh.Enabled) canhBao.Add("Sao lưu tự động đang TẮT - dữ liệu chỉ được bảo vệ khi có người bấm sao lưu.");
        else if (tuDongQuaHan) canhBao.Add($"Sao lưu tự động đã quá hạn (hạn {hanTuDong:dd/MM HH:mm}): dịch vụ nền có thể không chạy hoặc lần chạy gần nhất thất bại.");

        // Bản thành công gần nhất: đã kiểm tra chưa, còn file không, dung lượng
        bool? cuoiDaKiemTra = cuoi is null ? null : DaKiemTra(cuoi, lichSu);
        var cuoiConFile = cuoi?.DuongDanFile is { } p && File.Exists(p);
        if (cuoi is not null && !cuoiConFile) canhBao.Add("File của bản sao lưu gần nhất không còn trên máy chạy API (đã bị xóa hoặc di chuyển).");
        if (cuoi is not null && cuoiDaKiemTra == null) canhBao.Add("Bản sao lưu gần nhất chưa được kiểm tra toàn vẹn.");
        if (cuoiDaKiemTra == false) canhBao.Add("Bản sao lưu gần nhất KHÔNG đạt kiểm tra toàn vẹn - đừng dựa vào bản này.");

        // Dung lượng
        string? thuMuc = null; long? trong = null; string muc = "KHONG_RO"; string? ghiChuDl = null;
        try
        {
            thuMuc = await XacDinhThuMucAsync(builder, null);
            var goc = Path.GetPathRoot(thuMuc);
            if (!string.IsNullOrEmpty(goc)) trong = new DriveInfo(goc).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is SqlException or IOException or ArgumentException or ApiException) { }
        var kichThuocCsdl = await DocKichThuocCsdlAsync(builder);
        var banToiDa = thanhCong.Where(x => x.KichThuocBytes != null).Select(x => x.KichThuocBytes!.Value).DefaultIfEmpty(0).Max();
        var chuan = Math.Max(banToiDa, kichThuocCsdl ?? 0);
        if (trong is { } t)
        {
            var nguong = Math.Max(1L << 30, chuan * 3);
            muc = t < nguong ? "THAP" : "OK";
            var soLan = chuan > 0 ? t / chuan : (long?)null;
            ghiChuDl = $"Còn trống {Dinh(t)} ở ổ chứa thư mục sao lưu" + (soLan is { } n ? $" (đủ cho khoảng {n} bản nữa)" : "") + (muc == "THAP" ? " - SẮP HẾT, hãy dọn dẹp bản cũ hoặc đổi thư mục." : ".");
            if (muc == "THAP") canhBao.Add("Dung lượng lưu trữ sao lưu sắp hết.");
        }
        else ghiChuDl = "Không đọc được dung lượng trống của nơi lưu bản sao lưu.";

        var kq = new BackupStatusDto
        {
            TinhTrang = tinhTrang, TomTat = tomTat,
            LanSaoLuuThanhCongCuoi = cuoi?.ThoiGianThucHien, SoNgayTuLanSaoLuuCuoi = soNgay, DuongDanBanSaoLuuCuoi = cuoi?.DuongDanFile,
            TongSoBanSaoLuuThanhCong = thanhCong.Count,
            LanPhucHoiCuoi = lichSu.FirstOrDefault(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.PHUC_HOI && x.KetQua == KetQuaSaoLuu.THANH_CONG)?.ThoiGianThucHien,
            LanKiemTraCuoi = kiemTraCuoi?.ThoiGianThucHien,
            LanKiemTraCuoiHopLe = kiemTraCuoi is null ? null : kiemTraCuoi.KetQua == KetQuaSaoLuu.THANH_CONG,
            LanSaoLuuCuoiThatBai = lanCuoiThatBai,
            TuDongBat = lichTrinh.Enabled, ChuKyTuDongGio = lichTrinh.Enabled ? lichTrinh.IntervalHours : null, HanTuDongKeTiep = hanTuDong,
            TenCSDL = builder.InitialCatalog, CapNhatLuc = bayGio,
            LanSaoLuuCuoi = lanCuoi?.ThoiGianThucHien, KetQuaLanSaoLuuCuoi = lanCuoi?.KetQua.ToString(), LoiLanSaoLuuCuoi = lanCuoiThatBai ? (lanCuoi!.ThongBaoLoi ?? lanCuoi.GhiChu) : null,
            BanCuoiDaKiemTra = cuoiDaKiemTra, BanCuoiKichThuocBytes = cuoi?.KichThuocBytes, BanCuoiConFile = cuoiConFile,
            LanTuDongThanhCongCuoi = tuDong.FirstOrDefault(x => x.KetQua == KetQuaSaoLuu.THANH_CONG)?.ThoiGianThucHien,
            LanTuDongThatBaiCuoi = tuDong.FirstOrDefault(x => x.KetQua == KetQuaSaoLuu.THAT_BAI)?.ThoiGianThucHien,
            TuDongQuaHan = tuDongQuaHan,
            ThuMucSaoLuu = thuMuc, DungLuongTrongBytes = trong, KichThuocCSDLBytes = kichThuocCsdl,
            TongDungLuongCacBanBytes = thanhCong.Where(x => x.KichThuocBytes != null && x.DuongDanFile != null && File.Exists(x.DuongDanFile)).Sum(x => x.KichThuocBytes!.Value),
            MucDungLuong = muc, GhiChuDungLuong = ghiChuDl,
            CanhBao = canhBao
        };
        kq.KhaNangPhucHoi = await DanhGiaPhucHoiAsync(thanhCong, lichSu, lichTrinh, bayGio, kq.CanhBao);
        return kq;
    }

    private static string Dinh(long bytes)
    {
        string[] dv = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; var i = 0;
        while (v >= 1024 && i < dv.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {dv[i]}";
    }

    /// <summary>true = đã kiểm tra và đạt; false = kiểm tra không đạt; null = chưa kiểm tra lần nào.</summary>
    private static bool? DaKiemTra(LichSuSaoLuu ban, List<LichSuSaoLuu> lichSu)
    {
        var sauDo = lichSu.Where(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.KIEM_TRA && x.DuongDanFile == ban.DuongDanFile && x.ThoiGianThucHien >= ban.ThoiGianThucHien)
            .OrderByDescending(x => x.ThoiGianThucHien).FirstOrDefault();
        if (sauDo is not null) return sauDo.KetQua == KetQuaSaoLuu.THANH_CONG;
        return ban.DaKiemTraHopLe;
    }

    private async Task<long?> DocKichThuocCsdlAsync(SqlConnectionStringBuilder builder)
    {
        try
        {
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SUM(CAST(size AS bigint)) * 8192 FROM sys.database_files WHERE type = 0";
            var v = await cmd.ExecuteScalarAsync();
            return v is null or DBNull ? null : Convert.ToInt64(v);
        }
        catch (SqlException) { return null; }
    }

    private async Task<RecoveryAssessmentDto> DanhGiaPhucHoiAsync(List<LichSuSaoLuu> thanhCong, List<LichSuSaoLuu> lichSu, ScheduledBackupOptions lichTrinh, DateTime bayGio, List<string> canhBaoChung)
    {
        var dto = new RecoveryAssessmentDto
        {
            CoCheBaoVe =
            [
                "Phục hồi luôn kiểm tra toàn vẹn (checksum) và kiểm tra file thuộc đúng CSDL trước khi ghi đè",
                "Tự chụp bản sao lưu an toàn của dữ liệu hiện tại ngay trước khi phục hồi - không chụp được thì dừng",
                "Phải nhập lý do và gõ lại tên CSDL; mọi thao tác kiểm tra từ máy chủ (không thể bỏ qua bằng giao diện)",
                "Sau phục hồi tự kiểm tra CSDL (DBCC CHECKDB, số liệu, migration) và ghi lịch sử",
                "Lịch sử sao lưu được ghi thêm ra file ngoài CSDL nên không mất khi phục hồi"
            ]
        };
        // Bản đề xuất: bản thành công gần nhất còn file và không bị đánh dấu hỏng.
        LichSuSaoLuu? tot = null;
        foreach (var b in thanhCong)
        {
            if (string.IsNullOrEmpty(b.DuongDanFile) || !File.Exists(b.DuongDanFile)) continue;
            if (DaKiemTra(b, lichSu) == false) continue;
            tot = b; break;
        }
        if (tot is null)
        {
            dto.MucDo = "KHONG_CO";
            dto.KetLuan = "Hiện KHÔNG có bản sao lưu nào dùng được (chưa sao lưu, file đã mất hoặc không đạt kiểm tra). Nếu CSDL hỏng bây giờ, dữ liệu sẽ không phục hồi được - hãy sao lưu ngay.";
            dto.CanhBao.Add("Không có bản phục hồi khả dụng.");
            return dto;
        }

        var daKt = DaKiemTra(tot, lichSu);
        var gio = (int)Math.Floor((bayGio - tot.ThoiGianThucHien).TotalHours);
        dto.MaBanDeXuat = tot.MaLichSu; dto.ThoiDiemBanDeXuat = tot.ThoiGianThucHien; dto.DuongDanBanDeXuat = tot.DuongDanFile;
        dto.BanDeXuatDaKiemTra = daKt; dto.BanDeXuatConFile = true; dto.SoGioCoTheMatDuLieu = gio;
        dto.SoThaoTacCoTheMat = await _context.NhatKyThaoTacs.AsNoTracking().CountAsync(x => x.ThoiGian > tot.ThoiGianThucHien);

        var gioiHanGio = lichTrinh.Enabled ? Math.Max(24, lichTrinh.IntervalHours * 2) : 48;
        if (daKt == true && gio <= gioiHanGio) dto.MucDo = "SAN_SANG";
        else dto.MucDo = "RUI_RO";
        if (daKt != true) dto.CanhBao.Add(daKt == false ? "Bản đề xuất không đạt kiểm tra." : "Bản đề xuất chưa được kiểm tra toàn vẹn - hãy bấm Kiểm tra.");
        if (gio > gioiHanGio) dto.CanhBao.Add($"Bản đề xuất đã {gio} giờ tuổi - có thể mất nhiều dữ liệu.");
        if (!ReferenceEquals(tot, thanhCong.FirstOrDefault()))
            dto.CanhBao.Add("Bản sao lưu gần nhất không dùng được (mất file hoặc không đạt kiểm tra) nên đề xuất bản cũ hơn.");

        dto.KetLuan = $"Nếu CSDL hỏng ngay bây giờ: phục hồi bản #{tot.MaLichSu} ({tot.ThoiGianThucHien:dd/MM/yyyy HH:mm}). "
            + $"Dữ liệu có thể mất từ thời điểm đó đến nay ({(gio < 1 ? "dưới 1 giờ" : $"khoảng {gio} giờ")}, {dto.SoThaoTacCoTheMat} thao tác nghiệp vụ). "
            + (daKt == true ? "Bản này đã được kiểm tra toàn vẹn - đáng tin cậy." : "Bản này CHƯA được xác nhận toàn vẹn.");
        return dto;
    }
}
