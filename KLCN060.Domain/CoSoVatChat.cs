namespace KLCN060.Domain;

public class CoSoVatChat
{
    public string MaSo { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public string TinhTrang { get; set; } = null!;
    /// <summary>null = thiết bị trong kho / dùng chung (chưa gắn phòng).</summary>
    public string? MaPhong { get; set; }

    /// <summary>Ghi chú tự do (số seri, nhà cung cấp, bảo hành...).</summary>
    public string? GhiChu { get; set; }
    /// <summary>Thời điểm vào tình trạng hiện tại và lý do (để biết thiết bị hỏng bao lâu rồi, vì sao ngừng dùng).</summary>
    public DateTime? TinhTrangTuLuc { get; set; }
    public string? LyDoTinhTrang { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? CapNhatLuc { get; set; }
    /// <summary>Tên đăng nhập người tạo / sửa gần nhất.</summary>
    public string? NguoiCapNhat { get; set; }

    public Phong? Phong { get; set; }
}
