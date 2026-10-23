using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Màn Báo cáo (D17): từ điển chỉ số, đối chiếu/kiểm tra nhất quán, vấn đề cần chú ý, truy vết số liệu.
public sealed partial class ApiClient
{
    public Task<List<DinhNghiaChiSoDto>> GetDinhNghiaChiSoAsync() => GetAsync<List<DinhNghiaChiSoDto>>("api/v1/reports/definitions");
    public Task<DoiChieuDoanhThuDto> GetDoiChieuDoanhThuAsync(DateOnly tu, DateOnly den) => GetAsync<DoiChieuDoanhThuDto>($"api/v1/reports/reconciliation?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<VanDeBaoCaoTongDto> GetVanDeBaoCaoAsync(DateOnly tu, DateOnly den) => GetAsync<VanDeBaoCaoTongDto>($"api/v1/reports/insights?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<TruyVetBaoCaoDto> GetTruyVetBaoCaoAsync(string loai, DateOnly tu, DateOnly den) => GetAsync<TruyVetBaoCaoDto>($"api/v1/reports/drilldown?loai={Uri.EscapeDataString(loai)}&from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
}

public sealed partial class ApiClient
{
    /// <summary>Tải một tệp (báo cáo xuất) từ API: tự thử làm mới token một lần khi hết hạn.</summary>
    public async Task<(byte[] NoiDung, string TenFile)> TaiTepAsync(string duongDan, string tenMacDinh)
    {
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return (await responseMoi.Content.ReadAsByteArrayAsync(), responseMoi.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? tenMacDinh);
        }
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? tenMacDinh);
    }

    /// <summary>Xuất báo cáo quản trị của kỳ ra Excel (xlsx) hoặc PDF.</summary>
    public Task<(byte[] NoiDung, string TenFile)> XuatBaoCaoQuanTriAsync(DateOnly tu, DateOnly den, string dinhDang)
        => TaiTepAsync($"api/v1/reports/export?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}&dinhDang={Uri.EscapeDataString(dinhDang)}", $"KLCN060Hotel_BaoCaoQuanTri-{tu:dd.MM.yyyy}-{den:dd.MM.yyyy}_{DateTime.Now:dd.MM.yyyy_HHmm}.{dinhDang}");

    public Task<KetQuaTaiKhoanNhanVienDto> XoaNhanVienAsync(string maNV, string lyDo)
        => PostAsync<YeuCauLyDoTaiKhoan, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}/delete", new YeuCauLyDoTaiKhoan(lyDo));

    public Task<LichSaoLuuTuDongDto> GetLichSaoLuuTuDongAsync() => GetAsync<LichSaoLuuTuDongDto>("api/v1/system/backups/schedule");
    public Task<LichSaoLuuTuDongDto> DatLichSaoLuuTuDongAsync(bool bat, int chuKyGio) => PutAsync<object, LichSaoLuuTuDongDto>("api/v1/system/backups/schedule", new { tuDongBat = bat, chuKyGio });
}

public sealed class LichSaoLuuTuDongDto
{
    public bool TuDongBat { get; init; }
    public int ChuKyGio { get; init; } = 24;
    public int KiemTraMoiPhut { get; init; }
    public string? ThuMuc { get; init; }
}

public sealed class DinhNghiaChiSoDto
{
    public string Ma { get; init; } = ""; public string Ten { get; init; } = ""; public string CongThuc { get; init; } = ""; public string NguonDuLieu { get; init; } = ""; public string GhiChu { get; init; } = "";
    public string GiaiThich => string.IsNullOrEmpty(GhiChu) ? $"{CongThuc}." : $"{CongThuc}. {GhiChu}";
}

public sealed class KiemTraNhatQuanDto
{
    public string Ten { get; init; } = ""; public string VeTrai { get; init; } = ""; public string VePhai { get; init; } = ""; public bool Khop { get; init; } public string GhiChu { get; init; } = "";
    public string KetQua => Khop ? "✔ Khớp" : "✖ LỆCH";
    public string DongSo => $"{VeTrai}  =  {VePhai}" + (string.IsNullOrEmpty(GhiChu) ? "" : $"   ({GhiChu})");
}

public sealed class DoiChieuDoanhThuDto
{
    public decimal DoanhThuHoaDon { get; init; } public decimal DoanhThuHoaDonPhong { get; init; } public decimal DoanhThuPhongTheoDem { get; init; } public int SoDemPhongBan { get; init; }
    public int SoDemDaCoHoaDon { get; init; } public decimal TienDemDaCoHoaDon { get; init; } public int SoDemChuaCoHoaDon { get; init; } public decimal TienDemChuaCoHoaDon { get; init; }
    public string GiaiThich { get; init; } = ""; public List<KiemTraNhatQuanDto> KiemTra { get; init; } = [];
}

public sealed class VanDeBaoCaoDto
{
    public string Ma { get; init; } = ""; public string MucDo { get; init; } = ""; public string TieuDe { get; init; } = ""; public string MoTa { get; init; } = "";
    public string SoLieu { get; init; } = ""; public string HanhDong { get; init; } = ""; public string ManHinh { get; init; } = ""; public List<string> ChiTiet { get; init; } = [];
    public string MucDoHienThi => MucDo switch { "NGHIEM_TRONG" => "NGHIÊM TRỌNG", "CANH_BAO" => "CẢNH BÁO", _ => "THÔNG TIN" };
    public string ChiTietGop => string.Join("\n", ChiTiet.Select(x => "• " + x));
    public bool CoChiTiet => ChiTiet.Count > 0;
    public bool CoManHinh => !string.IsNullOrEmpty(ManHinh);
    public string TenManHinh => ManHinh switch
    {
        "PHONG" => "Mở Danh mục phòng", "BUONG_PHONG" => "Mở Buồng phòng", "DAT_PHONG" => "Mở Phiếu đặt phòng", "HOA_DON" => "Mở Hóa đơn", "DOI_SOAT" => "Mở Đối soát",
        "SAO_LUU" => "Mở Sao lưu", "NHAN_VIEN" => "Mở Phân quyền", _ => "Mở màn hình liên quan"
    };
}

public sealed class VanDeBaoCaoTongDto { public DateTime CapNhatLuc { get; init; } public List<VanDeBaoCaoDto> Items { get; init; } = []; }

public sealed class DongTruyVetDto
{
    public string Ma { get; init; } = ""; public DateTime? Ngay { get; init; } public string DoiTuong { get; init; } = ""; public string MoTa { get; init; } = ""; public decimal SoTien { get; init; } public string TrangThai { get; init; } = "";
    public string NgayHienThi => Ngay is { } d ? (d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm")) : "";
}

public sealed class TruyVetBaoCaoDto
{
    public string Loai { get; init; } = ""; public string Tieu { get; init; } = ""; public int SoDong { get; init; } public bool BiCatBot { get; init; } public decimal TongSoTien { get; init; }
    public decimal? TongKpi { get; init; } public bool? Khop { get; init; } public string GhiChu { get; init; } = ""; public List<DongTruyVetDto> Dong { get; init; } = [];
}
