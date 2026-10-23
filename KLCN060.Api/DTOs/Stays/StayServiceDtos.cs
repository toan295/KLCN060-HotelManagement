using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Stays;

public class AddStayServiceRequest
{
    public string MaDV { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
}

public class AddStayServicePackageRequest
{
    public string MaGoi { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
}

public class StayServiceUsageDto
{
    public int MaChiTietDV { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int SoLuong { get; set; }
    public DateTime NgaySuDung { get; set; }

    /// <summary>Gói dịch vụ mà dòng này thuộc về (null = dịch vụ lẻ).</summary>
    public string? MaGoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }

    /// <summary>Phòng mà dòng này được ghi cho (null = dòng cũ chưa biết phòng).</summary>
    public string? MaPhong { get; set; }

    /// <summary>Đơn giá đã chốt lúc ghi = thành tiền / số lượng.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    /// <summary>Tài khoản đã ghi dòng này (null = dữ liệu cũ).</summary>
    public string? NguoiGhi { get; set; }

    /// <summary>"Họ tên (Mã NV)" của người ghi; dòng nhập trước khi hệ thống lưu người ghi hiển thị "Hệ thống (dữ liệu cũ)". Không bao giờ để trống.</summary>
    public string NguoiGhiHienThi { get; set; } = "";

    /// <summary>Còn hủy được không (chỉ khi phiếu nhận chưa lập hóa đơn) và vì sao không.</summary>
    public bool CoTheHuy { get; set; } = true;
    public string? LyDoKhongHuy { get; set; }
}
