using KLCN060.Api.DTOs.System;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Sao lưu / phục hồi CSDL bằng T-SQL BACKUP/RESTORE DATABASE (SQL Server), không dùng công cụ ngoài.</summary>
public partial class BackupService : IBackupService
{
    // SQL Server báo lỗi 3201 khi không mở được file sao lưu (thường do tài khoản dịch vụ SQL Server không có quyền
    // ghi/đọc thư mục đó, VD thư mục trong Desktop/Documents của người dùng).
    private const int LoiKhongMoDuocThietBiSaoLuu = 3201;

    private readonly KLCN060DbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    public BackupService(KLCN060DbContext context, IConfiguration configuration, ILogger<BackupService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BackupSettingsDto> GetSettingsAsync()
    {
        var builder = LayConnectionStringBuilder();
        try
        {
            return new BackupSettingsDto { ThuMucMacDinh = await XacDinhThuMucAsync(builder, null) };
        }
        catch (Exception ex) when (ex is SqlException or IOException)
        {
            throw new ApiException(StatusCodes.Status500InternalServerError, "THIEU_CAU_HINH", ex.Message);
        }
    }

    public async Task<BackupHistoryDto> BackupAsync(BackupRequest? request, CurrentUser user, bool tuDong = false)
    {
        var builder = LayConnectionStringBuilder();
        var tenDb = builder.InitialCatalog;

        // Kiểm tra đầu vào TRƯỚC khối try để trả 400 đúng nghĩa, không ghi nhầm một dòng "thất bại" vào lịch sử.
        var thuMucChon = ChuanHoaThuMucNguoiDung(request?.ThuMuc);

        string? duongDan = null;
        string? ghiChu = tuDong ? "Sao lưu tự động theo lịch." : null;
        var dongHo = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var thuMuc = await XacDinhThuMucAsync(builder, thuMucChon);
            var tenFile = $"KLCN060_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..4]}.bak";
            duongDan = Path.Combine(thuMuc, tenFile);

            try
            {
                await SaoLuuRaFileAsync(builder, tenDb, duongDan);
            }
            catch (SqlException ex) when (LaLoiThietBiSaoLuu(ex))
            {
                // SQL Server (chạy bằng tài khoản dịch vụ riêng) không ghi được vào thư mục người dùng chọn.
                // API chạy bằng tài khoản người dùng nên ghi được: sao lưu vào thư mục mặc định của SQL Server
                // (luôn ghi được) rồi sao chép sang nơi đã chọn.
                _logger.LogWarning(ex, "SQL Server không ghi được thẳng vào {Path}, dùng thư mục trung gian", duongDan);
                // Tên file tạm riêng (GUID): nếu dùng lại tenFile thì có thể trùng và xóa nhầm bản sao lưu thật cùng tên trong thư mục mặc định.
                var tam = Path.Combine(await LayThuMucMacDinhSqlAsync(builder), $"KLCN060_tmp_{Guid.NewGuid():N}.bak");
                try
                {
                    await SaoLuuRaFileAsync(builder, tenDb, tam);
                    File.Copy(tam, duongDan, overwrite: true);
                    ghiChu = (ghiChu is null ? "" : ghiChu + " ") + "SQL Server không có quyền ghi thẳng vào thư mục này nên đã sao lưu qua thư mục trung gian rồi sao chép sang.";
                }
                finally
                {
                    XoaFileTam(tam);
                }
            }
        }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Sao lưu CSDL thất bại");
            await GhiLichSuAsync(LoaiThaoTacSaoLuu.SAO_LUU, user, duongDan, KetQuaSaoLuu.THAT_BAI, "Sao lưu thất bại.",
                new ChiTietGhi { TenCSDL = tenDb, ThoiLuongMs = (int)dongHo.ElapsedMilliseconds, Nguon = tuDong ? "TU_DONG" : "THU_CONG", Loi = ex.Message });
            throw new ApiException(StatusCodes.Status500InternalServerError, "SAO_LUU_THAT_BAI", $"Sao lưu thất bại: {ex.Message}");
        }

        // Chứng minh bản sao lưu có giá trị ngay sau khi tạo: file tồn tại, đọc được, đúng CSDL, checksum từng trang đạt.
        long? kichThuoc = null;
        try { kichThuoc = new FileInfo(duongDan!).Length; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var kiemTra = await KiemTraFileAsync(duongDan!, user, "Kiểm tra sau sao lưu", ghiLichSu: false);
        var banHopLe = kiemTra.HopLe && kiemTra.DungCSDL != false;
        ghiChu = (ghiChu is null ? "" : ghiChu + " ") + (banHopLe ? "Đã kiểm tra toàn vẹn: đạt." : $"CẢNH BÁO - kiểm tra toàn vẹn KHÔNG đạt: {kiemTra.ThongBao}");
        var lichSu = await GhiLichSuAsync(LoaiThaoTacSaoLuu.SAO_LUU, user, duongDan, KetQuaSaoLuu.THANH_CONG, ghiChu,
            new ChiTietGhi { TenCSDL = tenDb, KichThuoc = kichThuoc, ThoiLuongMs = (int)dongHo.ElapsedMilliseconds, Nguon = tuDong ? "TU_DONG" : "THU_CONG", DaKiemTra = banHopLe, Loi = banHopLe ? null : kiemTra.ThongBao });
        return ToDto(lichSu!);
    }

    public async Task<List<BackupHistoryDto>> GetHistoryAsync()
    {
        var ds = await _context.LichSuSaoLuus.AsNoTracking().OrderByDescending(x => x.ThoiGianThucHien).ThenByDescending(x => x.MaLichSu).Take(300).ToListAsync();
        var ten = await TenNguoiDung.HienThiAsync(_context, ds.Select(x => x.MaTaiKhoan));
        return ds.Select(x =>
        {
            var dto = ToDto(x);
            dto.NguoiLam = (x.NguonKichHoat == "TU_DONG" ? "Hệ thống (tự động)" : ten.GetValueOrDefault(x.MaTaiKhoan, x.MaTaiKhoan));
            if (x.LoaiThaoTac == LoaiThaoTacSaoLuu.SAO_LUU && x.KetQua == KetQuaSaoLuu.THANH_CONG && !string.IsNullOrEmpty(x.DuongDanFile))
                dto.FileTonTai = File.Exists(x.DuongDanFile);
            return dto;
        }).ToList();
    }

    public async Task<BackupHistoryDto> RestoreAsync(int maLichSu, RestoreRequest request, CurrentUser user)
    {
        var nguon = await LayBanSaoLuuPhucHoiAsync(maLichSu);
        return await PhucHoiAsync(nguon.DuongDanFile!, user, $"Phục hồi từ bản sao lưu #{maLichSu}", request, maLichSu);
    }

    /// <summary>Chữ ký cũ cho lời gọi trực tiếp trong tiến trình (không qua API/giao diện): tự điền lý do và xác nhận tên CSDL. API luôn đi qua phiên bản có <see cref="RestoreRequest"/> nên không bỏ qua được.</summary>
    public Task<BackupHistoryDto> RestoreAsync(int maLichSu, CurrentUser user)
        => RestoreAsync(maLichSu, new RestoreRequest { LyDo = "Phục hồi do chương trình gọi trực tiếp", XacNhanTenCSDL = LayConnectionStringBuilder().InitialCatalog }, user);

    public async Task<BackupHistoryDto> RestoreFromFileAsync(RestoreFileRequest request, CurrentUser user)
        => await PhucHoiAsync(ChuanHoaFileSaoLuu(request), user, "Phục hồi từ file do người dùng chọn",
            new RestoreRequest { LyDo = request.LyDo, XacNhanTenCSDL = request.XacNhanTenCSDL, BoQuaBanAnToan = request.BoQuaBanAnToan }, null);

    private async Task<LichSuSaoLuu> LayBanSaoLuuPhucHoiAsync(int maLichSu)
    {
        var nguon = await _context.LichSuSaoLuus.AsNoTracking().FirstOrDefaultAsync(x => x.MaLichSu == maLichSu)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LICH_SU_SAO_LUU", "Không tìm thấy bản ghi sao lưu.");
        if (nguon.LoaiThaoTac != LoaiThaoTacSaoLuu.SAO_LUU || nguon.KetQua != KetQuaSaoLuu.THANH_CONG || string.IsNullOrEmpty(nguon.DuongDanFile))
            throw new ApiException(StatusCodes.Status409Conflict, "BAN_SAO_LUU_KHONG_HOP_LE", "Chỉ có thể phục hồi từ một lần sao lưu thành công.");

        // File có thể đã bị xóa/di chuyển sau khi sao lưu - báo rõ thay vì để SQL Server trả lỗi khó hiểu.
        if (!File.Exists(nguon.DuongDanFile))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_FILE_SAO_LUU",
                $"Không tìm thấy file sao lưu tại \"{nguon.DuongDanFile}\" (có thể đã bị xóa hoặc di chuyển). Hãy dùng \"Phục hồi từ file...\" để chọn lại file.");
        return nguon;
    }

    /// <summary>Đường dẫn file .bak do người dùng nhập: tuyệt đối, đuôi .bak, phải tồn tại trên máy chạy API.</summary>
    private static string ChuanHoaFileSaoLuu(RestoreFileRequest request)
    {
        var duongDan = request.DuongDanFile?.Trim().Trim('"') ?? string.Empty;
        if (string.IsNullOrEmpty(duongDan) || !LaDuongDanTuyetDoiHopLe(duongDan))
            throw new ApiException(StatusCodes.Status400BadRequest, "DUONG_DAN_KHONG_HOP_LE", "Đường dẫn file sao lưu không hợp lệ (cần đường dẫn đầy đủ, ví dụ D:\\Sao luu\\KLCN060.bak).", "duongDanFile");
        if (!duongDan.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(StatusCodes.Status400BadRequest, "DUONG_DAN_KHONG_HOP_LE", "Chỉ hỗ trợ file có đuôi .bak.", "duongDanFile");
        if (!File.Exists(duongDan))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_FILE_SAO_LUU", $"Không tìm thấy file \"{duongDan}\" trên máy chạy API.", "duongDanFile");
        return duongDan;
    }

    public async Task<BackupVerifyResultDto> VerifyAsync(int maLichSu, CurrentUser user)
    {
        var nguon = await _context.LichSuSaoLuus.AsNoTracking().FirstOrDefaultAsync(x => x.MaLichSu == maLichSu)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LICH_SU_SAO_LUU", "Không tìm thấy bản ghi sao lưu.");
        if (nguon.LoaiThaoTac != LoaiThaoTacSaoLuu.SAO_LUU || nguon.KetQua != KetQuaSaoLuu.THANH_CONG || string.IsNullOrEmpty(nguon.DuongDanFile))
            throw new ApiException(StatusCodes.Status409Conflict, "BAN_SAO_LUU_KHONG_HOP_LE", "Chỉ kiểm tra được một lần sao lưu thành công.");

        // File có thể đã bị xóa/di chuyển: đó chính là một kết quả kiểm tra "không hợp lệ", không phải lỗi request.
        if (!File.Exists(nguon.DuongDanFile))
        {
            var mat = new BackupVerifyResultDto { HopLe = false, DuongDanFile = nguon.DuongDanFile, KiemTraLuc = DateTime.Now, ThongBao = $"Không tìm thấy file sao lưu tại \"{nguon.DuongDanFile}\" (đã bị xóa hoặc di chuyển)." };
            await GhiKetQuaKiemTraAsync(mat, user, $"Kiểm tra bản sao lưu #{maLichSu}", maLichSu);
            return mat;
        }

        return await KiemTraFileAsync(nguon.DuongDanFile, user, $"Kiểm tra bản sao lưu #{maLichSu}", ghiLichSu: true, maNguon: maLichSu);
    }

    public async Task<BackupVerifyResultDto> VerifyFileAsync(RestoreFileRequest request, CurrentUser user)
        => await KiemTraFileAsync(ChuanHoaFileSaoLuu(request), user, "Kiểm tra file do người dùng chọn", ghiLichSu: true);

    /// <summary>RESTORE VERIFYONLY ... WITH CHECKSUM: đọc toàn bộ file và kiểm tra checksum từng trang; RESTORE HEADERONLY: đọc CSDL gốc, thời điểm, dung lượng. Không đụng tới CSDL đang chạy.</summary>
    private async Task<BackupVerifyResultDto> KiemTraFileAsync(string duongDan, CurrentUser user, string ghiChu, bool ghiLichSu, int? maNguon = null)
    {
        var master = new SqlConnectionStringBuilder(LayConnectionStringBuilder().ConnectionString) { InitialCatalog = "master" };
        var tenDbHienTai = LayConnectionStringBuilder().InitialCatalog;
        string? tam = null;
        var hopLe = false;
        string thongBao;
        string? tenTrongFile = null; DateTime? luc = null; long? kichThuoc = null;
        try
        {
            await using var conn = new SqlConnection(master.ConnectionString);
            await conn.OpenAsync();
            var duongDanDoc = duongDan;
            try
            {
                await ChayAsync(conn, "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM", ("@path", duongDan));
            }
            catch (SqlException ex) when (LaLoiThietBiSaoLuu(ex))
            {
                // SQL Server không đọc được file ở vị trí này (thiếu quyền): chép sang thư mục mặc định của SQL Server rồi kiểm tra từ đó.
                tam = Path.Combine(await LayThuMucMacDinhSqlAsync(conn), $"KLCN060_verify_{Guid.NewGuid():N}.bak");
                File.Copy(duongDan, tam, overwrite: true);
                duongDanDoc = tam;
                await ChayAsync(conn, "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM", ("@path", tam));
            }
            (tenTrongFile, luc, kichThuoc) = await DocTieuDeFileAsync(conn, duongDanDoc);
            hopLe = true;
            thongBao = "File sao lưu toàn vẹn (checksum đạt), có thể dùng để phục hồi.";
            if (tenTrongFile is not null && !string.Equals(tenTrongFile, tenDbHienTai, StringComparison.OrdinalIgnoreCase))
                thongBao = $"File toàn vẹn nhưng thuộc CSDL \"{tenTrongFile}\", KHÔNG phải CSDL đang chạy \"{tenDbHienTai}\".";
        }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Kiểm tra file sao lưu {Path} không đạt", duongDan);
            thongBao = $"File sao lưu KHÔNG hợp lệ hoặc không đọc được: {ex.Message}";
        }
        finally
        {
            if (tam is not null) XoaFileTam(tam);
        }

        var kq = new BackupVerifyResultDto
        {
            HopLe = hopLe, DuongDanFile = duongDan, ThongBao = thongBao, KiemTraLuc = DateTime.Now,
            TenCSDLTrongFile = tenTrongFile, ThoiDiemSaoLuuTrongFile = luc, KichThuocBytes = kichThuoc,
            DungCSDL = tenTrongFile is null ? null : string.Equals(tenTrongFile, tenDbHienTai, StringComparison.OrdinalIgnoreCase)
        };
        if (ghiLichSu) await GhiKetQuaKiemTraAsync(kq, user, ghiChu, maNguon);
        return kq;
    }

    private static async Task<(string? Ten, DateTime? Luc, long? KichThuoc)> DocTieuDeFileAsync(SqlConnection conn, string duongDan)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.CommandText = "RESTORE HEADERONLY FROM DISK = @path";
        cmd.Parameters.AddWithValue("@path", duongDan);
        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync()) return (null, null, null);
        var iTen = rd.GetOrdinal("DatabaseName");
        var ten = rd.IsDBNull(iTen) ? null : Convert.ToString(rd.GetValue(iTen));
        var iLuc = rd.GetOrdinal("BackupFinishDate");
        DateTime? luc = rd.IsDBNull(iLuc) ? null : rd.GetDateTime(iLuc);
        var iKt = rd.GetOrdinal("BackupSize");
        long? kt = rd.IsDBNull(iKt) ? null : Convert.ToInt64(rd.GetValue(iKt));
        return (ten, luc, kt);
    }

    private async Task GhiKetQuaKiemTraAsync(BackupVerifyResultDto kq, CurrentUser user, string ghiChu, int? maNguon)
    {
        var dat = kq.HopLe && kq.DungCSDL != false;
        await GhiLichSuAsync(LoaiThaoTacSaoLuu.KIEM_TRA, user, kq.DuongDanFile, dat ? KetQuaSaoLuu.THANH_CONG : KetQuaSaoLuu.THAT_BAI, $"{ghiChu}: {kq.ThongBao}",
            new ChiTietGhi { TenCSDL = kq.TenCSDLTrongFile, KichThuoc = kq.KichThuocBytes, MaNguon = maNguon, DaKiemTra = dat, Loi = dat ? null : kq.ThongBao });
    }

    private async Task<BackupHistoryDto> PhucHoiAsync(string duongDanFile, CurrentUser user, string ghiChu, RestoreRequest request, int? maNguon)
    {
        var builder = LayConnectionStringBuilder();
        var tenDb = builder.InitialCatalog;

        // Phục hồi là thao tác nguy hiểm nhất của hệ thống nên được kiểm soát ở SERVER (không chỉ ở giao diện):
        // (0) phải có lý do và gõ lại đúng tên CSDL; (1) file phải toàn vẹn VÀ thuộc đúng CSDL - không bao giờ ghi đè bằng file hỏng / của CSDL khác;
        // (2) chụp "bản sao lưu an toàn" của CSDL hiện tại ngay trước khi ghi đè - không chụp được thì dừng, trừ khi người dùng chủ động chấp nhận rủi ro;
        // (3) sau khi phục hồi kiểm tra lại CSDL; (4) ghi lịch sử đầy đủ (kể cả khi thất bại).
        var lyDo = request.LyDo?.Trim();
        if (string.IsNullOrEmpty(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Phải nhập lý do phục hồi.", "lyDo");
        if (!string.Equals(request.XacNhanTenCSDL?.Trim(), tenDb, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(StatusCodes.Status400BadRequest, "XAC_NHAN_KHONG_DUNG", $"Để xác nhận ghi đè dữ liệu, hãy gõ đúng tên CSDL đang chạy: {tenDb}.", "xacNhanTenCSDL");

        var dongHo = System.Diagnostics.Stopwatch.StartNew();
        var kiemTra = await KiemTraFileAsync(duongDanFile, user, "Kiểm tra trước khi phục hồi", ghiLichSu: false);
        if (!kiemTra.HopLe)
            throw new ApiException(StatusCodes.Status409Conflict, "BAN_SAO_LUU_HONG", $"Không phục hồi vì file sao lưu không toàn vẹn. {kiemTra.ThongBao}");
        if (kiemTra.DungCSDL == false)
            throw new ApiException(StatusCodes.Status409Conflict, "BAN_SAO_LUU_KHAC_CSDL", kiemTra.ThongBao);

        var banAnToan = await TaoBanSaoLuuAnToanAsync(builder, tenDb, duongDanFile, user);
        if (banAnToan is null && !request.BoQuaBanAnToan)
            throw new ApiException(StatusCodes.Status409Conflict, "AN_TOAN_THAT_BAI",
                "Không tạo được bản sao lưu an toàn của CSDL hiện tại nên chưa phục hồi (để có đường quay lại nếu phục hồi nhầm). Nếu CSDL đang hỏng và bạn chấp nhận rủi ro, hãy xác nhận bỏ qua bước này.");
        ghiChu = banAnToan is null
            ? $"{ghiChu} (KHÔNG có bản sao lưu an toàn - người dùng chấp nhận rủi ro)"
            : $"{ghiChu}; bản sao lưu an toàn trước khi phục hồi: {banAnToan}";

        // RESTORE cần độc quyền CSDL đích nên phải chạy trên kết nối riêng tới master (không dùng DbContext của request).
        var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        var daChuyenSingleUser = false;
        string? tam = null;
        try
        {
            await using var conn = new SqlConnection(master.ConnectionString);
            await conn.OpenAsync();

            await ChayAsync(conn, $"ALTER DATABASE {QuoteName(tenDb)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            daChuyenSingleUser = true;
            try
            {
                try
                {
                    await ChayAsync(conn, $"RESTORE DATABASE {QuoteName(tenDb)} FROM DISK = @path WITH REPLACE, RECOVERY", ("@path", duongDanFile));
                }
                catch (SqlException ex) when (LaLoiThietBiSaoLuu(ex))
                {
                    // SQL Server không đọc được file ở vị trí này (thiếu quyền) - chép sang thư mục mặc định của SQL Server rồi phục hồi từ đó.
                    _logger.LogWarning(ex, "SQL Server không đọc được {Path}, dùng thư mục trung gian", duongDanFile);
                    tam = Path.Combine(await LayThuMucMacDinhSqlAsync(conn), $"KLCN060_restore_{Guid.NewGuid():N}.bak");
                    File.Copy(duongDanFile, tam, overwrite: true);
                    await ChayAsync(conn, $"RESTORE DATABASE {QuoteName(tenDb)} FROM DISK = @path WITH REPLACE, RECOVERY", ("@path", tam));
                }
            }
            finally
            {
                await ChayAsync(conn, $"ALTER DATABASE {QuoteName(tenDb)} SET MULTI_USER");
            }
        }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Phục hồi CSDL thất bại");
            SqlConnection.ClearAllPools();
            await GhiLichSuAsync(LoaiThaoTacSaoLuu.PHUC_HOI, user, duongDanFile, KetQuaSaoLuu.THAT_BAI, "Phục hồi thất bại.",
                new ChiTietGhi { TenCSDL = tenDb, LyDo = lyDo, MaNguon = maNguon, ThoiLuongMs = (int)dongHo.ElapsedMilliseconds, Loi = ex.Message });
            throw new ApiException(StatusCodes.Status500InternalServerError, "PHUC_HOI_THAT_BAI",
                $"Phục hồi thất bại{(daChuyenSingleUser ? " (đã trả CSDL về chế độ nhiều người dùng)" : string.Empty)}: {ex.Message}");
        }
        finally
        {
            if (tam is not null) XoaFileTam(tam);
        }

        // Các kết nối trong pool trỏ tới CSDL trước khi restore đã bị ngắt, cần bỏ đi.
        SqlConnection.ClearAllPools();
        _context.ChangeTracker.Clear();

        var kiemTraSau = await KiemTraSauPhucHoiAsync(builder, tenDb);
        // Lịch sử sao lưu nằm TRONG CSDL nên bị lùi về thời điểm bản sao lưu: nạp lại từ nhật ký ngoài CSDL để không mất dấu các thao tác đã làm.
        var daNap = await NapLaiLichSuTuNhatKyNgoaiAsync(user);
        if (daNap > 0) ghiChu += $"; đã nạp lại {daNap} dòng lịch sử sao lưu từ nhật ký ngoài CSDL";

        // Sau khi restore, CSDL là bản tại thời điểm sao lưu: tài khoản người gọi có thể không còn tồn tại.
        var lichSu = await GhiLichSuAsync(LoaiThaoTacSaoLuu.PHUC_HOI, user, duongDanFile, KetQuaSaoLuu.THANH_CONG, ghiChu,
            new ChiTietGhi { TenCSDL = tenDb, LyDo = lyDo, MaNguon = maNguon, ThoiLuongMs = (int)dongHo.ElapsedMilliseconds, DaKiemTra = kiemTraSau.DatYeuCau, Loi = kiemTraSau.DatYeuCau ? null : kiemTraSau.MoTa });
        var dto = lichSu is null
            ? new BackupHistoryDto
            {
                LoaiThaoTac = nameof(LoaiThaoTacSaoLuu.PHUC_HOI),
                ThoiGianThucHien = DateTime.Now,
                MaTaiKhoan = user.TenDN,
                DuongDanFile = duongDanFile,
                KetQua = nameof(KetQuaSaoLuu.THANH_CONG),
                TenCSDL = tenDb, LyDo = lyDo, MaBanNguon = maNguon,
                GhiChu = "Phục hồi thành công (không ghi được lịch sử vì tài khoản không tồn tại trong bản sao lưu)"
            }
            : ToDto(lichSu);
        dto.KiemTraSauPhucHoi = kiemTraSau.MoTa;
        return dto;
    }

    /// <summary>Sao lưu CSDL hiện tại sang một file riêng trước khi phục hồi. Không tạo được thì trả null (không chặn việc phục hồi: đó có thể chính là lúc CSDL đang hỏng).</summary>
    private async Task<string?> TaoBanSaoLuuAnToanAsync(SqlConnectionStringBuilder builder, string tenDb, string duongDanFileDangPhucHoi, CurrentUser user)
    {
        try
        {
            string thuMuc;
            try { thuMuc = await XacDinhThuMucAsync(builder, null); }
            catch (Exception ex) when (ex is SqlException or IOException) { thuMuc = Path.GetDirectoryName(duongDanFileDangPhucHoi)!; }

            var duongDan = Path.Combine(thuMuc, $"KLCN060_truocPhucHoi_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N")[..4]}.bak");
            try
            {
                await SaoLuuRaFileAsync(builder, tenDb, duongDan);
            }
            catch (SqlException ex) when (LaLoiThietBiSaoLuu(ex))
            {
                var tam = Path.Combine(await LayThuMucMacDinhSqlAsync(builder), $"KLCN060_tmp_{Guid.NewGuid():N}.bak");
                try { await SaoLuuRaFileAsync(builder, tenDb, tam); File.Copy(tam, duongDan, overwrite: true); }
                finally { XoaFileTam(tam); }
            }
            long? kt = null;
            try { kt = new FileInfo(duongDan).Length; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            var kiemTraAnToan = await KiemTraFileAsync(duongDan, user, "Kiểm tra bản an toàn", ghiLichSu: false);
            await GhiLichSuAsync(LoaiThaoTacSaoLuu.SAO_LUU, user, duongDan, KetQuaSaoLuu.THANH_CONG, "Bản sao lưu an toàn tự động chụp ngay trước khi phục hồi.",
                new ChiTietGhi { TenCSDL = tenDb, KichThuoc = kt, Nguon = "AN_TOAN", DaKiemTra = kiemTraAnToan.HopLe && kiemTraAnToan.DungCSDL != false });
            return duongDan;
        }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Không tạo được bản sao lưu an toàn trước khi phục hồi");
            return null;
        }
    }

    private SqlConnectionStringBuilder LayConnectionStringBuilder()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new ApiException(StatusCodes.Status500InternalServerError, "THIEU_CAU_HINH", "Thiếu chuỗi kết nối CSDL.");
        var builder = new SqlConnectionStringBuilder(cs);
        if (string.IsNullOrEmpty(builder.InitialCatalog))
            throw new ApiException(StatusCodes.Status500InternalServerError, "THIEU_CAU_HINH", "Chuỗi kết nối không có tên CSDL.");
        return builder;
    }

    /// <summary>Thư mục do người dùng nhập: bỏ khoảng trắng/dấu nháy, bắt buộc là đường dẫn tuyệt đối. Trả null nếu để trống.</summary>
    private static string? ChuanHoaThuMucNguoiDung(string? thuMuc)
    {
        thuMuc = thuMuc?.Trim().Trim('"');
        if (string.IsNullOrEmpty(thuMuc)) return null;

        if (!LaDuongDanTuyetDoiHopLe(thuMuc))
            throw new ApiException(StatusCodes.Status400BadRequest, "THU_MUC_KHONG_HOP_LE",
                "Thư mục sao lưu không hợp lệ (cần đường dẫn đầy đủ, ví dụ D:\\Sao luu KLCN060).", "thuMuc");
        // Đường dẫn đầy đủ của file (thư mục + tên file ~45 ký tự) không được vượt giới hạn 259 ký tự của Windows: chặn sớm bằng 400
        // thay vì để SQL Server báo lỗi rồi ghi một dòng sao lưu "thất bại" kèm 500.
        if (thuMuc.Length > 200)
            throw new ApiException(StatusCodes.Status400BadRequest, "THU_MUC_KHONG_HOP_LE", "Đường dẫn thư mục sao lưu quá dài (tối đa 200 ký tự).", "thuMuc");
        return thuMuc;
    }

    private static bool LaDuongDanTuyetDoiHopLe(string duongDan)
    {
        if (duongDan.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
        try { return Path.IsPathFullyQualified(duongDan); }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Thư mục lưu .bak: ưu tiên thư mục người dùng chọn, rồi cấu hình Backup:FolderPath, cuối cùng là thư mục backup mặc định của SQL Server.</summary>
    private async Task<string> XacDinhThuMucAsync(SqlConnectionStringBuilder builder, string? thuMucNguoiDung)
    {
        var laDoNguoiDungChon = !string.IsNullOrWhiteSpace(thuMucNguoiDung);
        var thuMuc = laDoNguoiDungChon ? thuMucNguoiDung : _configuration["Backup:FolderPath"];
        if (string.IsNullOrWhiteSpace(thuMuc))
            thuMuc = await LayThuMucMacDinhSqlAsync(builder);

        try { Directory.CreateDirectory(thuMuc); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            if (laDoNguoiDungChon)
                throw new ApiException(StatusCodes.Status400BadRequest, "THU_MUC_KHONG_HOP_LE", $"Không tạo/truy cập được thư mục \"{thuMuc}\": {ex.Message}", "thuMuc");
            _logger.LogWarning(ex, "Không tạo được thư mục sao lưu {Folder} từ tiến trình API", thuMuc);
        }

        return thuMuc;
    }

    private static async Task<string> LayThuMucMacDinhSqlAsync(SqlConnectionStringBuilder builder)
    {
        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        return await LayThuMucMacDinhSqlAsync(conn);
    }

    /// <summary>Dùng kết nối có sẵn - khi phục hồi, CSDL đang ở SINGLE_USER nên không được mở thêm kết nối mới vào nó.</summary>
    private static string TaoThuMucUngDung()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
        try { Directory.CreateDirectory(p); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return p;
    }

    private static async Task<string> LayThuMucMacDinhSqlAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(500))";
        var thuMuc = (await cmd.ExecuteScalarAsync()) as string;
        if (string.IsNullOrWhiteSpace(thuMuc))
            return TaoThuMucUngDung();   // LocalDB không có thư mục sao lưu mặc định: dùng thư mục của ứng dụng (nên đặt Backup:FolderPath khi triển khai thật)
        return thuMuc;
    }

    private static async Task SaoLuuRaFileAsync(SqlConnectionStringBuilder builder, string tenDb, string duongDan)
    {
        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await ChayAsync(conn, $"BACKUP DATABASE {QuoteName(tenDb)} TO DISK = @path WITH INIT, CHECKSUM", ("@path", duongDan));
    }

    private static bool LaLoiThietBiSaoLuu(SqlException ex)
        => ex.Errors.Cast<SqlError>().Any(e => e.Number == LoiKhongMoDuocThietBiSaoLuu);

    private void XoaFileTam(string duongDan)
    {
        try { if (File.Exists(duongDan)) File.Delete(duongDan); }
        catch (Exception ex) { _logger.LogWarning(ex, "Không xóa được file tạm {Path}", duongDan); }
    }

    private async Task<LichSuSaoLuu?> GhiLichSuAsync(LoaiThaoTacSaoLuu loai, CurrentUser user, string? duongDan, KetQuaSaoLuu ketQua, string? ghiChu, ChiTietGhi? ct = null)
    {
        ct ??= new ChiTietGhi();
        var entity = new LichSuSaoLuu
        {
            LoaiThaoTac = loai,
            ThoiGianThucHien = DateTime.Now,
            MaTaiKhoan = user.TenDN,
            DuongDanFile = duongDan,
            KetQua = ketQua,
            GhiChu = ghiChu is { Length: > 255 } ? ghiChu[..255] : ghiChu,
            TenCSDL = ct.TenCSDL ?? LayConnectionStringBuilder().InitialCatalog,
            KichThuocBytes = ct.KichThuoc,
            ThoiLuongMs = ct.ThoiLuongMs,
            NguonKichHoat = ct.Nguon,
            LyDo = ct.LyDo is { Length: > 255 } ? ct.LyDo[..255] : ct.LyDo,
            MaBanNguon = ct.MaNguon,
            DaKiemTraHopLe = ct.DaKiemTra,
            ThongBaoLoi = ct.Loi is { Length: > 1000 } ? ct.Loi[..1000] : ct.Loi
        };
        GhiNhatKyNgoai(entity);
        _context.LichSuSaoLuus.Add(entity);
        try
        {
            await _context.SaveChangesAsync();
            return entity;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Không ghi được LichSuSaoLuu");
            _context.Entry(entity).State = EntityState.Detached;
            return null;
        }
    }

    private sealed class ChiTietGhi
    {
        public string? TenCSDL { get; init; }
        public long? KichThuoc { get; init; }
        public int? ThoiLuongMs { get; init; }
        public string? Nguon { get; init; }
        public string? LyDo { get; init; }
        public int? MaNguon { get; init; }
        public bool? DaKiemTra { get; init; }
        public string? Loi { get; init; }
    }

    /// <summary>
    /// Helper thực thi câu lệnh SQL động cho BACKUP/RESTORE/ALTER DATABASE.
    /// [Khối 4 - Static Testing, review bảo mật 24/09/2026] CA2100 được tắt có chủ đích tại đây:
    /// các câu lệnh DDL này không hỗ trợ tham số hoá tên CSDL theo chuẩn SQL Server, nên tên CSDL
    /// được escape thủ công qua QuoteName() (nhân đôi dấu ']', đúng quy ước escape identifier của
    /// SQL Server) thay vì nối chuỗi trực tiếp không kiểm soát. Tên CSDL luôn lấy từ connection
    /// string cấu hình server (appsettings/Environment - xem LayConnectionStringBuilder), KHÔNG
    /// lấy từ input người dùng. Mọi giá trị do người dùng cung cấp (đường dẫn file .bak) đều đi
    /// qua tham số hoá chuẩn (SqlParameter qua AddWithValue), không nối chuỗi trực tiếp.
    /// Xem docs/testing/03-Khoi4-StaticTesting.md, mục "CA2100".
    /// </summary>
#pragma warning disable CA2100 // Đã review (xem doc comment phía trên): identifier escape hợp lệ, giá trị người dùng luôn tham số hoá.
    private static async Task ChayAsync(SqlConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }
#pragma warning restore CA2100

    private static string QuoteName(string ten) => $"[{ten.Replace("]", "]]")}]";

    private static BackupHistoryDto ToDto(LichSuSaoLuu x) => new()
    {
        MaLichSu = x.MaLichSu,
        LoaiThaoTac = x.LoaiThaoTac.ToString(),
        ThoiGianThucHien = x.ThoiGianThucHien,
        MaTaiKhoan = x.MaTaiKhoan,
        DuongDanFile = x.DuongDanFile,
        KetQua = x.KetQua.ToString(),
        GhiChu = x.GhiChu,
        TenCSDL = x.TenCSDL, KichThuocBytes = x.KichThuocBytes, ThoiLuongMs = x.ThoiLuongMs, NguonKichHoat = x.NguonKichHoat,
        LyDo = x.LyDo, MaBanNguon = x.MaBanNguon, DaKiemTraHopLe = x.DaKiemTraHopLe, ThongBaoLoi = x.ThongBaoLoi
    };
}
