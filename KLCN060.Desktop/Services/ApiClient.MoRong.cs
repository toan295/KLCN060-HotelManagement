using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Các lời gọi API mở rộng cho Quản lý: gói dịch vụ, sửa phiếu đặt phòng (ngoại lệ).
public sealed partial class ApiClient
{
    // ---------------- Gói dịch vụ ----------------


    // ---------------- Sửa phiếu đặt phòng (lễ tân hoặc Quản lý): đổi ngày, đổi phòng, đổi loại phòng ----------------
    public Task<ThongTinNhanPhongDto> GetThongTinNhanPhongAsync(string maPhieuDat)
        => GetAsync<ThongTinNhanPhongDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/check-in-info");
    public Task<ChiTietPhieuDatDto> SuaDatPhongAsync(string maPhieuDat, YeuCauSuaDatPhong x)
        => PutAsync<YeuCauSuaDatPhong, ChiTietPhieuDatDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}", x);

    /// <summary>Xem trước thay đổi (máy chủ kiểm tra phòng trống/giá/khuyến mãi như khi sửa thật nhưng không lưu gì).</summary>
    /// <summary>Phòng đổi được cho một phòng đang giữ: cùng loại khách đã đặt, còn trống trong khoảng ngày (Quản lý và lễ tân đều dùng được).</summary>
    public Task<List<PhongTrongDto>> GetPhongDoiDuocAsync(string maPhieuDat, string maPhongCu, DateOnly nhan, DateOnly tra)
        => GetAsync<List<PhongTrongDto>>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/swap-rooms?maPhongCu={Uri.EscapeDataString(maPhongCu)}&nhan={nhan:yyyy-MM-dd}&tra={tra:yyyy-MM-dd}");
    public Task<XemTruocSuaPhieuDto> XemTruocSuaDatPhongAsync(string maPhieuDat, YeuCauSuaDatPhong x)
        => PostAsync<YeuCauSuaDatPhong, XemTruocSuaPhieuDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/preview-update", x);
}

public sealed class XemTruocSuaPhongDto
{
    public string MaPhong { get; init; } = "";
    public string MaLoai { get; init; } = "";
    public string? DoiTuPhong { get; init; }
    public decimal DonGiaCu { get; init; }
    public decimal DonGiaMoi { get; init; }
    public decimal ThanhTienCu { get; init; }
    public decimal ThanhTienMoi { get; init; }
    public string HienThi => (DoiTuPhong is null ? $"Phòng {MaPhong} (giữ nguyên)" : $"Phòng {MaPhong} (đổi từ {DoiTuPhong})")
        + $": {DonGiaCu:N0}đ → {DonGiaMoi:N0}đ/đêm · thành tiền {ThanhTienCu:N0}đ → {ThanhTienMoi:N0}đ";
}

/// <summary>Kết quả xem trước một lần sửa phiếu: lễ tân đọc cho khách rồi mới xác nhận (không phải tự tính).</summary>
public sealed class XemTruocSuaPhieuDto
{
    public string MaPhieuDat { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public DateOnly NgayDonCu { get; init; }
    public DateOnly NgayTraCu { get; init; }
    public DateOnly NgayDonMoi { get; init; }
    public DateOnly NgayTraMoi { get; init; }
    public int SoDemCu { get; init; }
    public int SoDemMoi { get; init; }
    public decimal TongCu { get; init; }
    public decimal TongMoi { get; init; }
    public decimal ChenhLech { get; init; }
    public decimal KhuyenMaiCu { get; init; }
    public decimal KhuyenMaiMoi { get; init; }
    public decimal TienCocCu { get; init; }
    public decimal TienCocMoi { get; init; }
    public bool DaThuCoc { get; init; }
    public decimal CocLechSoVoiChuan { get; init; }
    public bool HuyMaQrChoCoc { get; init; }
    public List<XemTruocSuaPhongDto> Phongs { get; init; } = [];

    public string TomTat
    {
        get
        {
            var d = new System.Text.StringBuilder();
            d.AppendLine($"Ngày ở: {NgayDonCu:dd/MM/yyyy} - {NgayTraCu:dd/MM/yyyy} ({SoDemCu} đêm)  →  {NgayDonMoi:dd/MM/yyyy} - {NgayTraMoi:dd/MM/yyyy} ({SoDemMoi} đêm)");
            d.AppendLine();
            foreach (var p in Phongs) d.AppendLine(p.HienThi);
            d.AppendLine();
            d.AppendLine($"Tổng tiền phòng: {TongCu:N0}đ → {TongMoi:N0}đ  (chênh lệch {(ChenhLech >= 0 ? "+" : "")}{ChenhLech:N0}đ)");
            if (KhuyenMaiCu != KhuyenMaiMoi) d.AppendLine($"Khuyến mãi: -{KhuyenMaiCu:N0}đ → -{KhuyenMaiMoi:N0}đ");
            if (!DaThuCoc)
                d.AppendLine(TienCocCu == TienCocMoi ? $"Tiền cọc (chưa thu): {TienCocMoi:N0}đ, không đổi."
                    : $"Tiền cọc (chưa thu): {TienCocCu:N0}đ → {TienCocMoi:N0}đ" + (HuyMaQrChoCoc ? " - mã QR chuyển khoản cũ sẽ bị hủy, khách cần mã mới." : "."));
            else
            {
                d.AppendLine($"Tiền cọc ĐÃ THU: {TienCocMoi:N0}đ, giữ nguyên.");
                if (CocLechSoVoiChuan > 0) d.AppendLine($"50% tổng mới là {TienCocMoi + CocLechSoVoiChuan:N0}đ → nên thu thêm {CocLechSoVoiChuan:N0}đ (hệ thống KHÔNG tự thu thêm).");
                else if (CocLechSoVoiChuan < 0) d.AppendLine($"Cọc đã thu cao hơn 50% tổng mới {-CocLechSoVoiChuan:N0}đ (hệ thống KHÔNG tự hoàn; trừ vào hóa đơn khi trả phòng).");
            }
            return d.ToString().TrimEnd();
        }
    }
}

// ---------------- Gói dịch vụ ----------------
// ---------------- Sửa đặt phòng ----------------
public sealed record YeuCauSuaDatPhong(DateOnly? NgayDonDuKien, DateOnly? NgayTraDuKien, List<YeuCauDoiPhongDat>? DoiPhong, string LyDo);
