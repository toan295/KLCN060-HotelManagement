using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Bookings;

// ------------------------------------------------------------------------------------------------------------------
// Tổng quan một phiếu đặt cho Quản lý: nhìn một phiếu là hiểu đủ tình trạng (khách, ở ngày nào, bao nhiêu người, phòng gì,
// nguồn, thanh toán, ai tạo / ai sửa, lịch sử gần nhất) và biết thao tác nào được / không được làm (kèm lý do).
// ------------------------------------------------------------------------------------------------------------------

public class BookingRoomOverviewDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;
    public int Tang { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaApDung { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }

    /// <summary>Tình trạng phòng hiện tại (VC, VD, VI, OC, OD, OOO).</summary>
    public string TinhTrangPhong { get; set; } = null!;

    /// <summary>CHUA_NHAN, DANG_O hoặc DA_TRA (theo lượt lưu trú thực tế của phiếu).</summary>
    public string LuuTru { get; set; } = "CHUA_NHAN";
    public int? SoNguoi { get; set; }
}

public class BookingInvoiceOverviewDto
{
    public string MaHD { get; set; } = null!;
    public DateTime NgayLap { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DaThu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ConNo { get; set; }
    public string TrangThai { get; set; } = null!;
}

public class BookingActionDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public bool ChoPhep { get; set; }

    /// <summary>Khi không được phép: vì sao. Khi được phép: điều cần lưu ý (VD bắt buộc nhập lý do).</summary>
    public string GiaiThich { get; set; } = "";
    public bool BatBuocLyDo { get; set; }
}

public class BookingHistoryItemDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? ChiTiet { get; set; }
}

public class BookingOverviewDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string NguonDat { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;

    // Khách
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string SoDTKhach { get; set; } = null!;
    public string? EmailKhach { get; set; }
    public string? CCCDKhach { get; set; }
    public int SoNguoi { get; set; }
    public int SoNguoiOCung { get; set; }

    // Lưu trú
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public int SoDem { get; set; }
    public List<BookingRoomOverviewDto> Phong { get; set; } = new();
    public string? MaPhieuNhan { get; set; }

    // Tiền
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongKhuyenMai { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongSauKhuyenMai { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }
    public bool DaThuCoc { get; set; }
    public DateTime? NgayThuCoc { get; set; }
    public string? HinhThucCoc { get; set; }
    public string? NguoiThuCoc { get; set; }

    /// <summary>Tổng tiền đã thu của phiếu: tiền cọc đã nhận (nếu chưa hủy) + các khoản thanh toán hóa đơn.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DaThu { get; set; }

    /// <summary>Còn phải thu: (tổng sau khuyến mãi hoặc tổng hóa đơn) trừ đã thu; không âm.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ConLai { get; set; }

    /// <summary>CHUA_COC, DA_COC, CON_NO, DA_THANH_TOAN, DA_HUY_HOAN_COC, MAT_COC, DA_HUY_KHONG_COC.</summary>
    public string TrangThaiThanhToan { get; set; } = null!;
    public string TomTatThanhToan { get; set; } = "";
    public List<BookingInvoiceOverviewDto> HoaDon { get; set; } = new();

    // Ai tạo / ai sửa
    public DateTime NgayTao { get; set; }
    public string NguoiTao { get; set; } = null!;
    public DateTime? NgaySuaCuoi { get; set; }
    public string? NguoiSuaCuoi { get; set; }
    public int SoLanSua { get; set; }

    // Hủy / không đến
    public DateTime? NgayHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? HuyBoi { get; set; }

    // Chính sách hủy cho Quản lý khi cân nhắc: nếu hủy bây giờ thì sao
    public int SoNgayConLaiToiNgayDon { get; set; }
    public bool HoanCocNeuHuyBayGio { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTienHoanNeuHuy { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienGiuLaiNeuHuy { get; set; }

    public bool QuaHanChuaNhan { get; set; }
    public List<BookingActionDto> ThaoTac { get; set; } = new();
    public List<BookingHistoryItemDto> LichSuGanNhat { get; set; } = new();
}
