using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Reports;

/// <summary>Định nghĩa một chỉ số báo cáo: công thức, nguồn dữ liệu và các trường hợp đặc biệt đã được quy ước.</summary>
public class ReportDefinitionDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public string CongThuc { get; set; } = null!;
    public string NguonDuLieu { get; set; } = null!;
    public string GhiChu { get; set; } = "";
}

/// <summary>Một phép kiểm tra nhất quán giữa hai số liệu phải bằng nhau (hoặc lệch tối đa một ngưỡng làm tròn).</summary>
public class ReportCheckDto
{
    public string Ten { get; set; } = null!;
    public string VeTrai { get; set; } = null!;
    public string VePhai { get; set; } = null!;
    public bool Khop { get; set; }
    public string GhiChu { get; set; } = "";
}

/// <summary>Giải thích vì sao "doanh thu hóa đơn" khác "doanh thu phòng theo đêm ở" của cùng kỳ.</summary>
public class ReportReconciliationDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuHoaDonPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuPhongTheoDem { get; set; }
    public int SoDemPhongBan { get; set; }

    /// <summary>Đêm ở thuộc lượt đã có hóa đơn đã thu (được ghi nhận vào doanh thu hóa đơn vào ngày lập hóa đơn).</summary>
    public int SoDemDaCoHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDemDaCoHoaDon { get; set; }

    /// <summary>Đêm ở của lượt chưa có hóa đơn đã thu (đang ở, chưa trả phòng hoặc chưa thanh toán): phát sinh nhưng chưa vào doanh thu hóa đơn.</summary>
    public int SoDemChuaCoHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDemChuaCoHoaDon { get; set; }

    public string GiaiThich { get; set; } = "";
    public List<ReportCheckDto> KiemTra { get; set; } = new();
}

/// <summary>Một vấn đề quản lý cần chú ý: cái gì, mức độ, số liệu, làm gì, mở màn hình nào.</summary>
public class ReportInsightDto
{
    public string Ma { get; set; } = null!;

    /// <summary>NGHIEM_TRONG, CANH_BAO hoặc THONG_TIN.</summary>
    public string MucDo { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string MoTa { get; set; } = null!;

    /// <summary>Giá trị đo được và ngưỡng (đã định dạng), VD "14,5% (ngưỡng 40%)".</summary>
    public string SoLieu { get; set; } = "";
    public string HanhDong { get; set; } = "";

    /// <summary>Nhóm màn hình để điều hướng (PHONG, DAT_PHONG, HOA_DON, BUONG_PHONG, DOI_SOAT, SAO_LUU...).</summary>
    public string ManHinh { get; set; } = "";
    public List<string> ChiTiet { get; set; } = new();
}

public class ReportInsightsDto
{
    public DateTime CapNhatLuc { get; set; }
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public List<ReportInsightDto> Items { get; set; } = new();
}

public class ReportDrillRowDto
{
    public string Ma { get; set; } = "";
    public DateTime? Ngay { get; set; }
    public string DoiTuong { get; set; } = "";
    public string MoTa { get; set; } = "";
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    public string TrangThai { get; set; } = "";
}

/// <summary>Các dòng dữ liệu tạo nên một chỉ số: cộng lại phải ra đúng số trên KPI.</summary>
public class ReportDrillDto
{
    public string Loai { get; set; } = null!;
    public string Tieu { get; set; } = null!;
    public string Khoa { get; set; } = "";
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int SoDong { get; set; }
    public bool BiCatBot { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongSoTien { get; set; }

    /// <summary>Giá trị tương ứng trên KPI (khi không lọc thêm) để đối chiếu; null = không áp dụng.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal? TongKpi { get; set; }
    public bool? Khop { get; set; }
    public string GhiChu { get; set; } = "";
    public List<ReportDrillRowDto> Dong { get; set; } = new();
}
