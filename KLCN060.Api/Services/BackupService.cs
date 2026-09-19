using KLCN060.Api.DTOs.System;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Sao lưu / phục hồi CSDL bằng T-SQL BACKUP/RESTORE DATABASE (SQL Server), không dùng công cụ ngoài.</summary>
public class BackupService : IBackupService
{
    private readonly KLCN060DbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    public BackupService(KLCN060DbContext context, IConfiguration configuration, ILogger<BackupService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BackupHistoryDto> BackupAsync(CurrentUser user)
    {
        var builder = LayConnectionStringBuilder();
        var tenDb = builder.InitialCatalog;

        string? duongDan = null;
        try
        {
            var thuMuc = await XacDinhThuMucAsync(builder);
            duongDan = Path.Combine(thuMuc, $"KLCN060_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandText = $"BACKUP DATABASE {QuoteName(tenDb)} TO DISK = @path WITH INIT, CHECKSUM";
            cmd.Parameters.AddWithValue("@path", duongDan);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is SqlException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Sao lưu CSDL thất bại");
            await GhiLichSuAsync(LoaiThaoTacSaoLuu.SAO_LUU, user, duongDan, KetQuaSaoLuu.THAT_BAI, ex.Message);
            throw new ApiException(StatusCodes.Status500InternalServerError, "SAO_LUU_THAT_BAI", $"Sao lưu thất bại: {ex.Message}");
        }

        var lichSu = await GhiLichSuAsync(LoaiThaoTacSaoLuu.SAO_LUU, user, duongDan, KetQuaSaoLuu.THANH_CONG, null);
        return ToDto(lichSu!);
    }

    public async Task<List<BackupHistoryDto>> GetHistoryAsync()
    {
        var ds = await _context.LichSuSaoLuus.AsNoTracking().OrderByDescending(x => x.ThoiGianThucHien).ThenByDescending(x => x.MaLichSu).ToListAsync();
        return ds.Select(ToDto).ToList();
    }

    public async Task<BackupHistoryDto> RestoreAsync(int maLichSu, CurrentUser user)
    {
        var nguon = await _context.LichSuSaoLuus.AsNoTracking().FirstOrDefaultAsync(x => x.MaLichSu == maLichSu)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LICH_SU_SAO_LUU", "Không tìm thấy bản ghi sao lưu.");
        if (nguon.LoaiThaoTac != LoaiThaoTacSaoLuu.SAO_LUU || nguon.KetQua != KetQuaSaoLuu.THANH_CONG || string.IsNullOrEmpty(nguon.DuongDanFile))
            throw new ApiException(StatusCodes.Status409Conflict, "BAN_SAO_LUU_KHONG_HOP_LE", "Chỉ có thể phục hồi từ một lần sao lưu thành công.");

        var builder = LayConnectionStringBuilder();
        var tenDb = builder.InitialCatalog;

        // RESTORE cần độc quyền CSDL đích nên phải chạy trên kết nối riêng tới master (không dùng DbContext của request).
        var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        var daChuyenSingleUser = false;
        try
        {
            await using var conn = new SqlConnection(master.ConnectionString);
            await conn.OpenAsync();

            await ChayAsync(conn, $"ALTER DATABASE {QuoteName(tenDb)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            daChuyenSingleUser = true;
            try
            {
                await ChayAsync(conn, $"RESTORE DATABASE {QuoteName(tenDb)} FROM DISK = @path WITH REPLACE, RECOVERY", ("@path", nguon.DuongDanFile));
            }
            finally
            {
                await ChayAsync(conn, $"ALTER DATABASE {QuoteName(tenDb)} SET MULTI_USER");
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Phục hồi CSDL thất bại");
            SqlConnection.ClearAllPools();
            await GhiLichSuAsync(LoaiThaoTacSaoLuu.PHUC_HOI, user, nguon.DuongDanFile, KetQuaSaoLuu.THAT_BAI, ex.Message);
            throw new ApiException(StatusCodes.Status500InternalServerError, "PHUC_HOI_THAT_BAI",
                $"Phục hồi thất bại{(daChuyenSingleUser ? " (đã trả CSDL về chế độ nhiều người dùng)" : string.Empty)}: {ex.Message}");
        }

        // Các kết nối trong pool trỏ tới CSDL trước khi restore đã bị ngắt, cần bỏ đi.
        SqlConnection.ClearAllPools();
        _context.ChangeTracker.Clear();

        // Sau khi restore, CSDL là bản tại thời điểm sao lưu: tài khoản người gọi có thể không còn tồn tại.
        var lichSu = await GhiLichSuAsync(LoaiThaoTacSaoLuu.PHUC_HOI, user, nguon.DuongDanFile, KetQuaSaoLuu.THANH_CONG, $"Phục hồi từ bản sao lưu #{maLichSu}");
        return lichSu is null
            ? new BackupHistoryDto
            {
                LoaiThaoTac = nameof(LoaiThaoTacSaoLuu.PHUC_HOI),
                ThoiGianThucHien = DateTime.Now,
                MaTaiKhoan = user.TenDN,
                DuongDanFile = nguon.DuongDanFile,
                KetQua = nameof(KetQuaSaoLuu.THANH_CONG),
                GhiChu = "Phục hồi thành công (không ghi được lịch sử vì tài khoản không tồn tại trong bản sao lưu)"
            }
            : ToDto(lichSu);
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

    /// <summary>Thư mục lưu .bak: cấu hình Backup:FolderPath, nếu trống dùng thư mục backup mặc định của SQL Server (chắc chắn có quyền ghi).</summary>
    private async Task<string> XacDinhThuMucAsync(SqlConnectionStringBuilder builder)
    {
        var thuMuc = _configuration["Backup:FolderPath"];
        if (string.IsNullOrWhiteSpace(thuMuc))
        {
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(500))";
            thuMuc = (await cmd.ExecuteScalarAsync()) as string;
        }
        if (string.IsNullOrWhiteSpace(thuMuc))
            throw new IOException("Chưa cấu hình Backup:FolderPath và SQL Server không cung cấp thư mục sao lưu mặc định.");

        try { Directory.CreateDirectory(thuMuc); }
        catch (Exception ex) { _logger.LogWarning(ex, "Không tạo được thư mục sao lưu {Folder} từ tiến trình API", thuMuc); }

        return thuMuc;
    }

    private async Task<LichSuSaoLuu?> GhiLichSuAsync(LoaiThaoTacSaoLuu loai, CurrentUser user, string? duongDan, KetQuaSaoLuu ketQua, string? ghiChu)
    {
        var entity = new LichSuSaoLuu
        {
            LoaiThaoTac = loai,
            ThoiGianThucHien = DateTime.Now,
            MaTaiKhoan = user.TenDN,
            DuongDanFile = duongDan,
            KetQua = ketQua,
            GhiChu = ghiChu is { Length: > 255 } ? ghiChu[..255] : ghiChu
        };
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

    private static async Task ChayAsync(SqlConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string QuoteName(string ten) => $"[{ten.Replace("]", "]]")}]";

    private static BackupHistoryDto ToDto(LichSuSaoLuu x) => new()
    {
        MaLichSu = x.MaLichSu,
        LoaiThaoTac = x.LoaiThaoTac.ToString(),
        ThoiGianThucHien = x.ThoiGianThucHien,
        MaTaiKhoan = x.MaTaiKhoan,
        DuongDanFile = x.DuongDanFile,
        KetQua = x.KetQua.ToString(),
        GhiChu = x.GhiChu
    };
}
