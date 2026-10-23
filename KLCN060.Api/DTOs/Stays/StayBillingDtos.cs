using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Stays;

public class ChangeRoomRequest
{
    public string MaPhongMoi { get; set; } = null!;
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột PhieuDoiPhong.GhiChu
    public string? GhiChu { get; set; }
}

public class ChangeRoomResultDto
{
    public string MaPhieuDoi { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhongCu { get; set; } = null!;
    public string MaPhongMoi { get; set; } = null!;
    public DateTime NgayDoi { get; set; }

    /// <summary>Chênh lệch đơn giá/đêm (phòng mới trừ phòng cũ), có thể âm.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ChenhLechGia { get; set; }

    public string TinhTrangPhongCu { get; set; } = null!;
    public string TinhTrangPhongMoi { get; set; } = null!;

    /// <summary>NANG_HANG (đơn giá phòng mới cao hơn), HA_HANG (thấp hơn) hoặc NGANG_HANG (bằng nhau).</summary>
    public string LoaiDoi { get; set; } = null!;
}

/// <summary>Xem trước đổi phòng (không ghi gì): ai đổi từ phòng nào sang phòng nào, hạng/giá, chênh lệch, tác động tiền, có cần duyệt không và vì sao chưa đổi được.</summary>
public class ChangeRoomPreviewDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string MaPhongCu { get; set; } = null!;
    public string MaLoaiCu { get; set; } = null!;
    public string TenLoaiCu { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGiaCu { get; set; }
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTraDuKien { get; set; }
    public int SoDemConLai { get; set; }
    public int SoNguoi { get; set; }
    public string TrangThaiLuot { get; set; } = null!;

    public string? MaPhongMoi { get; set; }
    public string? MaLoaiMoi { get; set; }
    public string? TenLoaiMoi { get; set; }
    public string? TinhTrangPhongMoi { get; set; }
    public int SucChuaPhongMoi { get; set; }
    /// <summary>Đơn giá phòng mới theo bảng giá hôm nay.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGiaBangGia { get; set; }
    /// <summary>Đơn giá sẽ áp dụng (theo bảng giá hôm nay).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGiaApDung { get; set; }
    /// <summary>NGANG_HANG / NANG_HANG / HA_HANG.</summary>
    public string? LoaiDoi { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ChenhLechMoiDem { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThuNguoiCuMoiDem { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThuNguoiMoiMoiDem { get; set; }
    /// <summary>Tổng tiền thay đổi cho những đêm còn lại (dương = khách trả thêm, âm = giảm), đã gồm phụ thu người ở thêm.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TacDongTong { get; set; }
    public bool CanLyDo { get; set; }
    public bool CoTheDoi { get; set; }
    public string? MaLoi { get; set; }
    public string? LyDoKhongDoi { get; set; }
    public string? HuongDan { get; set; }
    public List<string> CanhBao { get; set; } = new();
}

/// <summary>Xem trước gia hạn: số đêm thêm, tiền thêm và ngày tối đa gia hạn được (trước phiếu đặt kế tiếp của phòng).</summary>
public class ExtendPreviewDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string? HoTenKhach { get; set; }
    public DateOnly? NgayTraDuKienCu { get; set; }
    public DateOnly NgayTraMoi { get; set; }
    public int SoDemThem { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienThemDuKien { get; set; }
    public bool CoTheGiaHan { get; set; }
    public string? MaLoi { get; set; }
    public string? LyDoKhongGiaHan { get; set; }
    /// <summary>Phiếu đặt kế tiếp đang giữ phòng này (nếu có) và ngày bắt đầu: không gia hạn quá ngày đó được.</summary>
    public string? MaPhieuDatKeTiep { get; set; }
    public DateOnly? NgayToiDaGiaHan { get; set; }
}

public class ExtendStayRequest
{
    /// <summary>Ngày trả dự kiến mới: phải sau ngày trả dự kiến hiện tại (và sau hôm nay).</summary>
    public DateOnly NgayTraMoi { get; set; }

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class ExtendStayResultDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public DateOnly? NgayTraDuKienCu { get; set; }
    public DateOnly NgayTraDuKienMoi { get; set; }
    public int SoDemThem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    /// <summary>Tiền phòng dự kiến phát sinh thêm = số đêm thêm × đơn giá đêm đang áp dụng cho lượt ở này.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienThemDuKien { get; set; }
}

public class CheckOutRequest
{
    /// <summary>Số "còn phải thu" mà lễ tân đã nhìn thấy khi bấm xác nhận. Nếu folio đã đổi (vừa ghi dịch vụ, vừa đổi phòng...) thì từ chối để xem lại; null = không kiểm.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal? ConPhaiThuDaXem { get; set; }

    /// <summary>Lễ tân tick khi khách trả phòng trễ giờ và cần tính phụ thu 1 đêm.</summary>
    public bool ApDungPhuThuTreGio { get; set; }
}

public class FolioSummaryDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDichVu { get; set; }

    /// <summary>Tiền phòng + phụ thu + dịch vụ (chưa trừ khuyến mãi và tiền cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongCong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongKhuyenMai { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDaCoc { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConPhaiThu { get; set; }
}

public class FolioLineDto
{
    public string LoaiKhoanMuc { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public int SoLuong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThanhTien { get; set; }
}

public class FolioDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public FolioSummaryDto TongHopFolio { get; set; } = null!;
    public List<FolioLineDto> ChiTiet { get; set; } = new();
}

public class CheckOutResultDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string TinhTrangPhong { get; set; } = null!;

    /// <summary>Null khi phiếu nhận còn phòng khác đang ở — hóa đơn chỉ được lập khi trả phòng cuối cùng.</summary>
    public string? MaHoaDon { get; set; }

    public FolioSummaryDto TongHopFolio { get; set; } = null!;

    public DateTime ThoiGianTra { get; set; }
    /// <summary>Tài khoản thực hiện trả phòng (cũng có trong nhật ký TRA_PHONG).</summary>
    public string? NguoiThucHien { get; set; }
    /// <summary>Số phòng khác của cùng phiếu nhận còn khách (hóa đơn chỉ lập khi trả phòng cuối).</summary>
    public int SoPhongConDangO { get; set; }
    public string? TrangThaiPhieuDat { get; set; }
    /// <summary>Trạng thái hóa đơn vừa lập (CHUA_THANH_TOAN / DA_THANH_TOAN): 0đ phải thu thì đã thanh toán.</summary>
    public string? TrangThaiHoaDon { get; set; }
    public int SoDemO { get; set; }
}

/// <summary>Một điều kiện cần đạt trước khi trả phòng và kết quả kiểm tra hiện tại.</summary>
public class CheckOutConditionDto
{
    public string Ten { get; set; } = null!;
    public bool Dat { get; set; }
    /// <summary>true = không đạt thì KHÔNG cho trả phòng; false = chỉ cảnh báo.</summary>
    public bool Chan { get; set; }
    public string? ChiTiet { get; set; }
}

public class CheckOutOtherRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public string? GhiChu { get; set; }
}

/// <summary>Xem trước trả phòng (không ghi gì): đối tượng, ngày ở, folio có phụ thu, điều kiện trả phòng, cách giải thích số tiền còn phải thu.</summary>
public class CheckOutInfoDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string? SoDTKhach { get; set; }
    public int SoNguoi { get; set; }
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTraDuKien { get; set; }
    public DateOnly HomNay { get; set; }
    public int SoDemO { get; set; }
    /// <summary>DUNG_HAN / SOM (trước ngày trả dự kiến) / TRE (quá ngày trả dự kiến) / CHUA_KHAI.</summary>
    public string ThoiDiemTra { get; set; } = null!;
    public int SoNgayLech { get; set; }
    public string TrangThaiLuot { get; set; } = null!;
    public string? MaHoaDonDaCo { get; set; }
    public bool LaPhongCuoi { get; set; }
    public List<CheckOutOtherRoomDto> PhongKhac { get; set; } = new();

    public FolioSummaryDto TongHopFolio { get; set; } = null!;
    public List<FolioLineDto> ChiTiet { get; set; } = new();
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThuTreGioApDung { get; set; }
    /// <summary>Giải thích bằng chữ vì sao "còn phải thu" ra số này (đặc biệt khi bằng 0đ).</summary>
    public string GiaiThichConPhaiThu { get; set; } = null!;

    public List<CheckOutConditionDto> DieuKien { get; set; } = new();
    public bool DuocTra { get; set; }
    public string? LyDoKhongTra { get; set; }
    public List<string> CanhBao { get; set; } = new();
    public DateTime TinhLuc { get; set; }
}

public class StaySummaryDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string? SoDTKhach { get; set; }
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }

    /// <summary>Ngày trả dự kiến (theo phiếu đặt hoặc do lễ tân khai/gia hạn); null nếu chưa biết.</summary>
    public DateOnly? NgayTraDuKien { get; set; }
    public int SoNguoi { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    public string TrangThai { get; set; } = null!;
}
