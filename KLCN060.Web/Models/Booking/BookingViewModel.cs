namespace KLCN060.Web.Models.Booking;

/// <summary>Khớp với BookingDto bên KLCN060.Api - dùng cho W8 (xác nhận cọc) và W10 (chi tiết đơn).</summary>
public class BookingViewModel
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public decimal TienCoc { get; set; }
    public decimal TongTienDuKien { get; set; }
    public decimal TongKhuyenMai { get; set; }
    public int SoDem { get; set; }
    /// <summary>Số khách khai khi đặt (null nếu không khai, VD đặt đoàn).</summary>
    public int? SoKhach { get; set; }
    public int SoKhachVuotTieuChuan { get; set; }
    /// <summary>Phụ thu dự kiến do khách vượt tiêu chuẩn; chưa nằm trong tiền cọc, tính vào hóa đơn khi trả phòng.</summary>
    public decimal PhuThuDuKien { get; set; }

    /// <summary>Cọc đã hoàn / bị giữ lại khi đơn bị hủy (cả hai bằng 0 nghĩa là đơn hủy khi chưa đóng cọc).</summary>
    public decimal SoTienHoanCoc { get; set; }
    public decimal TienCocGiuLai { get; set; }
    public List<HeldRoomViewModel> PhongDaGiu { get; set; } = new();
}

public class HeldRoomViewModel
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = "";
    /// <summary>Tên loại phòng (Standard, Superior...) để hiển thị cho khách; thiếu thì dùng mã loại.</summary>
    public string LoaiHienThi => string.IsNullOrWhiteSpace(TenLoai) ? MaLoai : TenLoai;
    public decimal DonGiaApDung { get; set; }
    public decimal ThanhTien { get; set; }
}
