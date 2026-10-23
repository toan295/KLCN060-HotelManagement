using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class DichVu
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public decimal GiaDV { get; set; }
    public string DonViTinh { get; set; } = null!;

    /// <summary>false = ngừng phục vụ: không ghi thêm cho khách được nữa và ẩn khỏi danh sách công khai (lượt đã ghi vẫn giữ nguyên).</summary>
    public bool HoatDong { get; set; } = true;
    /// <summary>Nhóm doanh thu của dịch vụ (báo cáo kế toán: dịch vụ / minibar / giặt là / khác).</summary>
    public LoaiDoanhThuDichVu LoaiDoanhThu { get; set; } = LoaiDoanhThuDichVu.DICH_VU;

    public ICollection<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; } = new List<ChiTietSuDungDV>();
    public ICollection<ChiTietDichVu> ChiTietDichVus { get; set; } = new List<ChiTietDichVu>();
    public ICollection<ChiTietGoiDichVu> ChiTietGois { get; set; } = new List<ChiTietGoiDichVu>();
}
