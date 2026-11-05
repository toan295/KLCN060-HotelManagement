namespace KLCN060.Desktop.Services;

public sealed class NhatKyDto
{
    public long MaNhatKy { get; init; }
    public DateTime ThoiGian { get; init; }
    public string TaiKhoan { get; init; } = "";
    public string? NguoiThucHien { get; init; }
    public string HanhDong { get; init; } = "";
    public string MoTaHanhDong { get; init; } = "";
    public string? DoiTuongTacDong { get; init; }
    public string? ChiTiet { get; init; }
    public string KetQua { get; init; } = "";
    public string? MaLoi { get; init; }
    public string? DiaChiIP { get; init; }

    public string NguoiHienThi => string.IsNullOrEmpty(NguoiThucHien) ? TaiKhoan : NguoiThucHien;
    public string KetQuaHienThi => KetQua switch { "THANH_CONG" => "Thành công", "THAT_BAI" => "Thất bại", "TU_CHOI" => "Bị từ chối", _ => KetQua };
}

public partial class ApiClient
{
    /// <summary>Nhật ký hệ thống (Quản lý): lọc theo ngày / tài khoản / từ khóa / kết quả, mới nhất trước, có phân trang.</summary>
    public Task<KetQuaTrangDto<NhatKyDto>> GetNhatKyAsync(DateTime? tuNgay, DateTime? denNgay, string? taiKhoan, string? tuKhoa, string? ketQua, int trang, int soDong = 50)
    {
        var ps = new List<string> { $"page={trang}", $"pageSize={soDong}" };
        if (tuNgay is not null) ps.Add($"tuNgay={tuNgay:yyyy-MM-dd}");
        if (denNgay is not null) ps.Add($"denNgay={denNgay:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(taiKhoan)) ps.Add($"taiKhoan={Uri.EscapeDataString(taiKhoan.Trim())}");
        if (!string.IsNullOrWhiteSpace(tuKhoa)) ps.Add($"q={Uri.EscapeDataString(tuKhoa.Trim())}");
        if (!string.IsNullOrEmpty(ketQua)) ps.Add($"ketQua={ketQua}");
        return GetTrangAsync<NhatKyDto>("api/v1/audit-logs?" + string.Join("&", ps));
    }
}
