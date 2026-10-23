using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Đồ khách bỏ quên (Lost &amp; Found): Đã tìm thấy → Đang lưu giữ → Đã trả khách / Đã thanh lý.</summary>
public class DoBoQuen
{
    public int MaDo { get; set; }

    /// <summary>Phòng phát hiện (null khi nhặt ở khu vực chung - khi đó phải có <see cref="KhuVuc"/>).</summary>
    public string? MaPhong { get; set; }
    public string? KhuVuc { get; set; }
    public DateTime ThoiGianPhatHien { get; set; }
    public LoaiDoBoQuen Loai { get; set; }
    public string MoTa { get; set; } = null!;

    /// <summary>Họ tên người nhặt (mặc định là người ghi nhận).</summary>
    public string NguoiNhat { get; set; } = null!;

    /// <summary>Tên đăng nhập người ghi nhận.</summary>
    public string NguoiGhiNhan { get; set; } = null!;
    public string? ViTriLuuGiu { get; set; }
    public TrangThaiDoBoQuen TrangThai { get; set; }

    /// <summary>Lượt lưu trú gần nhất của phòng tại thời điểm phát hiện - để lễ tân biết liên hệ ai (chỉ người có quyền xem khách mới thấy tên).</summary>
    public string? MaPhieuNhan { get; set; }

    public DateTime? ThoiGianTra { get; set; }
    public string? NguoiNhanLai { get; set; }
    public string? GiayToNguoiNhan { get; set; }
    public string? NguoiTra { get; set; }
    public string? GhiChuTra { get; set; }

    public DateTime? ThoiGianThanhLy { get; set; }
    public string? NguoiThanhLy { get; set; }
    public string? LyDoThanhLy { get; set; }

    public Phong? Phong { get; set; }
    public ICollection<AnhDoBoQuen> Anhs { get; set; } = new List<AnhDoBoQuen>();
}

public class AnhDoBoQuen
{
    public int MaAnh { get; set; }
    public int MaDo { get; set; }
    public string TenFile { get; set; } = null!;
    public long KichThuocByte { get; set; }
    public DateTime ThoiGian { get; set; }

    public DoBoQuen DoBoQuen { get; set; } = null!;
}
