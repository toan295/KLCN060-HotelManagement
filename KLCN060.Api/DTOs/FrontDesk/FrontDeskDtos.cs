using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.FrontDesk;

/// <summary>Con số nhìn nhanh của một ca trực lễ tân: phòng, khách đến/đi hôm nay và các việc cần xử lý.</summary>
public class FrontDeskOverviewDto
{
    public DateOnly Ngay { get; set; }
    public DateTime CapNhatLuc { get; set; }

    // ----- Phòng (theo tình trạng buồng phòng hiện tại) -----
    public int TongSoPhong { get; set; }

    /// <summary>Phòng trống sạch (VC) và đã kiểm tra (VI): sẵn sàng đón khách.</summary>
    public int PhongTrong { get; set; }

    /// <summary>Trong số phòng trống sạch: còn bao nhiêu phòng chưa được giữ cho khách đến hôm nay (bán được ngay cho khách vãng lai).</summary>
    public int PhongTrongBanDuoc { get; set; }

    /// <summary>Phòng đang có khách (OC + OD).</summary>
    public int PhongDangCoKhach { get; set; }

    /// <summary>Phòng trống nhưng chưa dọn (VD) - buồng phòng cần dọn.</summary>
    public int PhongChoDon { get; set; }

    /// <summary>Phòng có khách đang ở nhưng cần dọn (OD).</summary>
    public int PhongCoKhachCanDon { get; set; }

    /// <summary>Số phòng bẩn đang có buồng phòng nhận dọn (đã bấm "nhận dọn", chưa xong).</summary>
    public int PhongDangDon { get; set; }

    /// <summary>Phòng đang bảo trì / khóa (OOO).</summary>
    public int PhongBaoTri { get; set; }

    // ----- Khách đến -----

    /// <summary>Số phiếu đặt có ngày đón hôm nay (mọi trạng thái còn hiệu lực + đã nhận + đã trả).</summary>
    public int BookingDenHomNay { get; set; }

    /// <summary>Trong số đó: còn chờ nhận phòng (chưa nhận), gồm phòng sắp check-in.</summary>
    public int BookingChoNhanPhong { get; set; }
    public int PhongSapCheckIn { get; set; }
    public int BookingDaNhanHomNay { get; set; }

    // ----- Khách đi -----

    /// <summary>Phòng có ngày trả dự kiến hôm nay và khách còn ở (sắp check-out).</summary>
    public int PhongSapCheckOut { get; set; }
    public int PhongDaTraHomNay { get; set; }

    /// <summary>Phòng khách còn ở nhưng đã quá ngày trả dự kiến.</summary>
    public int PhongQuaHanTra { get; set; }

    public int KhachDangLuuTru { get; set; }

    // ----- Phòng: cân đối (TongSoPhong = PhongTrongBanDuoc + PhongGiuChoKhachDen + PhongChoDon + PhongDangCoKhach + PhongBaoTri) -----

    /// <summary>Phòng trống sạch (VC/VI) nhưng đã được giữ cho khách đến hôm nay: chưa bán được cho khách vãng lai.</summary>
    public int PhongGiuChoKhachDen { get; set; }

    // ----- Tiền cọc và công nợ -----

    /// <summary>Phiếu chờ xác nhận cọc có ngày đón hôm nay hoặc ngày mai (cần thu cọc trước khi nhận phòng).</summary>
    public int DatCocChoThu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienCocChoThu { get; set; }

    /// <summary>Hóa đơn còn nợ (Tổng tiền - tổng các lần đã thu &gt; 0), cùng cách tính với màn Hóa đơn.</summary>
    public int HoaDonConNo { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongConPhaiThu { get; set; }

    // ----- Không đến, việc chờ khác -----

    /// <summary>Phiếu quá ngày đón, chưa nhận phòng và chưa được đánh dấu "không đến".</summary>
    public int KhongDenChoXuLy { get; set; }
    public int SuCoDangMo { get; set; }
    public int MinibarChoTinhPhi { get; set; }
    public int DoBoQuenDangLuu { get; set; }

    /// <summary>Số BOOKING (đếm một lần mỗi phiếu) đang có việc cần xử lý: chờ cọc, quá ngày đón, phòng giữ chưa sẵn sàng.</summary>
    public int BookingCanXuLy { get; set; }

    /// <summary>Số việc phát sinh từ lượt lưu trú / hóa đơn / phòng (không phải booking): quá hạn trả, hóa đơn chưa thu, bảo trì, sự cố...</summary>
    public int ViecKhacCanXuLy { get; set; }

    public List<FrontDeskTaskDto> CanXuLy { get; set; } = new();
}

/// <summary>Một việc lễ tân cần xử lý (booking hoặc lượt lưu trú).</summary>
public class FrontDeskTaskDto
{
    /// <summary>CHO_COC | QUA_HAN_KHONG_DEN | TRA_PHONG_QUA_HAN | PHONG_CHUA_SAN_SANG | HOA_DON_CHUA_THU | HOA_DON_CON_NUA | PHONG_BAO_TRI | SU_CO_MO | MINIBAR_CHO_TINH_PHI | DO_BO_QUEN</summary>
    public string Loai { get; set; } = null!;

    /// <summary>NGHIEM_TRONG | CANH_BAO | THONG_TIN</summary>
    public string MucDo { get; set; } = null!;
    public string MaThamChieu { get; set; } = null!;
    public string MoTa { get; set; } = null!;

    /// <summary>Màn hình xử lý tương ứng: DAT_PHONG | NHAN_PHONG | TRA_PHONG | THANH_TOAN | SO_DO_PHONG | BUONG_PHONG.</summary>
    public string ManHinhDich { get; set; } = null!;

    /// <summary>Tên hành động ngắn gọn cho nút (VD "Mở booking · thu cọc").</summary>
    public string HanhDong { get; set; } = null!;
    public string? MaPhong { get; set; }
    public string? MaPhieuNhan { get; set; }
}

public class ArrivalRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TinhTrang { get; set; } = null!;
    public bool DaNhan { get; set; }

    /// <summary>Phòng đã dọn sạch và kiểm tra, sẵn sàng giao khách.</summary>
    public bool SanSang { get; set; }
}

public class ArrivalDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string SoDTKhach { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string NguonDat { get; set; } = null!;
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }

    /// <summary>Đã thu cọc (phiếu ở trạng thái đã xác nhận trở đi).</summary>
    public bool DaCoc { get; set; }
    public int SoKhachDiCung { get; set; }
    public List<ArrivalRoomDto> Phongs { get; set; } = new();

    /// <summary>Có thể làm thủ tục nhận phòng ngay: đã cọc, chưa nhận đủ và mọi phòng chưa nhận đã sẵn sàng.</summary>
    public bool CoTheNhanNgay { get; set; }
}

public class DepartureDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string? SoDTKhach { get; set; }
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTraDuKien { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoNguoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    /// <summary>DANG_O hoặc DA_TRA_PHONG.</summary>
    public string TrangThai { get; set; } = null!;

    /// <summary>Đang ở nhưng đã quá ngày trả dự kiến.</summary>
    public bool QuaHan { get; set; }
}

public class VacantRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }
    public string TinhTrang { get; set; } = null!;

    /// <summary>Nhận phòng ngay hôm nay được: khoảng tìm bắt đầu hôm nay và phòng đã dọn (VC/VI).</summary>
    public bool NhanNgayDuoc { get; set; }
}

public class NoShowReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }

    /// <summary>Phiếu đã được đánh dấu "không đến" trong kỳ (theo ngày đánh dấu).</summary>
    public List<Bookings.BookingSummaryDto> DaDanhDau { get; set; } = new();

    /// <summary>Phiếu quá ngày đón, chưa nhận phòng và chưa được đánh dấu: lễ tân cần xử lý (đánh dấu không đến hoặc liên hệ khách).</summary>
    public List<Bookings.BookingSummaryDto> ChoXuLy { get; set; } = new();
}
