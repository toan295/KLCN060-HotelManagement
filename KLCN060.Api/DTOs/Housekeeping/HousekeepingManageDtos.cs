using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Housekeeping;

/// <summary>Một phòng trên bảng điều hành buồng phòng: đang ở giai đoạn nào của vòng đời Bẩn → Giao việc → Đang dọn → Chờ kiểm tra → Sẵn sàng.</summary>
public class HousekeepingRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TenLoaiPhong { get; set; } = null!;
    public string TinhTrang { get; set; } = null!;

    /// <summary>BAN (chưa ai nhận), DA_GIAO (đã giao, chưa bắt đầu), DANG_DON, CHO_KIEM_TRA (VC), SAN_SANG (VI), CO_KHACH (OC), BAN_CO_KHACH, BAO_TRI (OOO).</summary>
    public string GiaiDoan { get; set; } = null!;
    public DateTime? TuLuc { get; set; }
    public double? SoGioCho { get; set; }

    /// <summary>KHAN (giữ cho khách đến hôm nay mà chưa sẵn sàng), CAO (chờ quá ngưỡng), BINH_THUONG.</summary>
    public string MucUuTien { get; set; } = "BINH_THUONG";
    public string? LyDoUuTien { get; set; }

    public string? NguoiDon { get; set; }
    public string? HoTenNguoiDon { get; set; }
    public DateTime? BatDauDonLuc { get; set; }
    public string? NguoiPhanCong { get; set; }
    public DateTime? GiaoLuc { get; set; }

    /// <summary>Phòng đã dọn xong (chờ kiểm tra / sẵn sàng): ai dọn và dọn xong lúc nào (lấy từ phiếu dọn gần nhất đã hoàn thành).</summary>
    public string? NguoiDonXong { get; set; }
    public string? HoTenNguoiDonXong { get; set; }
    public DateTime? DonXongLuc { get; set; }

    /// <summary>Khách sắp đến phòng này (hôm nay hoặc ngày mai): tên + mã phiếu đặt + ngày đón.</summary>
    public string? KhachSapDen { get; set; }
    public string? MaPhieuDatSapDen { get; set; }
    public DateOnly? NgayDonSapDen { get; set; }

    public int SuCoDangMo { get; set; }

    /// <summary>Chỉ phòng bảo trì (OOO): lý do khóa phòng, ai khóa, từ lúc nào, đã bao nhiêu giờ.</summary>
    public string? LyDoKhoa { get; set; }
    public string? NguoiKhoa { get; set; }
}

public class HousekeepingStaffRowDto
{
    public string TenDN { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string VaiTro { get; set; } = null!;
    public bool DangHoatDong { get; set; }

    /// <summary>Các phòng đang dọn (đã bắt đầu).</summary>
    public List<string> DangDon { get; set; } = new();

    /// <summary>Các phòng được giao nhưng chưa bắt đầu.</summary>
    public List<string> DaGiaoChuaBatDau { get; set; } = new();

    /// <summary>Số phòng đang dọn quá ngưỡng thời gian (chưa xong).</summary>
    public int QuaGio { get; set; }
    public int DaXongHomNay { get; set; }
    public double? ThoiGianTrungBinhPhut { get; set; }
}

public class HousekeepingOverviewDto
{
    public DateTime CapNhatLuc { get; set; }
    public int TongPhong { get; set; }

    // Số phòng theo tình trạng thô
    public int VC { get; set; }
    public int VD { get; set; }
    public int VI { get; set; }
    public int OC { get; set; }
    public int OD { get; set; }
    public int OOO { get; set; }

    // Số phòng theo giai đoạn công việc
    public int ChuaAiNhan { get; set; }
    public int DaGiaoChuaBatDau { get; set; }
    public int DangDon { get; set; }
    public int ChoKiemTra { get; set; }
    public int SanSang { get; set; }
    public int PhongKhan { get; set; }
    public int DaDonHomNay { get; set; }
    public int DaKiemTraHomNay { get; set; }

    // Việc cần chú ý ở các nghiệp vụ khác của buồng phòng
    public int SuCoDangMo { get; set; }
    public int SuCoCaoKhanCap { get; set; }
    public int MinibarChoTinhPhi { get; set; }
    public int DoBoQuenDangLuu { get; set; }

    /// <summary>Phòng cần xử lý (bẩn, đã giao, đang dọn, chờ kiểm tra) xếp theo mức ưu tiên.</summary>
    public List<HousekeepingRoomDto> CanXuLy { get; set; } = new();

    /// <summary>Phòng khách sắp đến (hôm nay / ngày mai) kèm tình trạng sẵn sàng, để biết phòng nào cần ưu tiên.</summary>
    public List<HousekeepingRoomDto> SapCheckIn { get; set; } = new();

    public List<HousekeepingRoomDto> PhongKhoa { get; set; } = new();
    public List<HousekeepingStaffRowDto> NhanVien { get; set; } = new();
}

public class AssignCleaningRequest
{
    [Required(ErrorMessage = "{0} là bắt buộc.")]
    [StringLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NguoiDon { get; set; } = null!;
}

/// <summary>Một dòng của nhật ký buồng phòng: ai làm gì, trên phòng nào, lúc nào, trạng thái trước → sau.</summary>
public class HousekeepingActivityDto
{
    /// <summary>Mỗi thay đổi một dòng "Nhãn: trước → sau" (tách từ chi tiết thao tác); rỗng nếu thao tác không có thay đổi trước / sau.</summary>
    public string? ThayDoiChu { get; set; }

    /// <summary>Thông tin đi kèm thao tác, mỗi ý một dòng "Nhãn: giá trị" bằng tiếng Việt.</summary>
    public string? ChiTietDeDoc { get; set; }

    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = null!;
    public string? HoTenNguoiThucHien { get; set; }
    public string HanhDong { get; set; } = null!;
    public string? MaPhong { get; set; }
    public string? Truoc { get; set; }
    public string? Sau { get; set; }
    public string? LyDo { get; set; }
    public string? ChiTiet { get; set; }
}
