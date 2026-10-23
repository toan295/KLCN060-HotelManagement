using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Roles;

public class PermissionDto
{
    public int MaQuyen { get; set; }
    public string TenQuyen { get; set; } = null!;
    public string? NhomChucNang { get; set; }
    /// <summary>Mô tả quyền cho phép làm gì (hiển thị ở D15).</summary>
    public string? MoTa { get; set; }

    /// <summary>Tên quyền bằng ngôn ngữ nghiệp vụ (người quản trị không cần nhớ mã kỹ thuật).</summary>
    public string TenHienThi { get; set; } = "";
    public string NhomHienThi { get; set; } = "";

    /// <summary>THUONG, NHAY_CAM hoặc RAT_NHAY_CAM (quyền tiền, duyệt, quản trị).</summary>
    public string MucNhayCam { get; set; } = "THUONG";
}

/// <summary>Quyền hiện tại của người đang đăng nhập (theo phân quyền trong CSDL) — Desktop dùng để hiện/ẩn chức năng.</summary>
public class MyPermissionsDto
{
    public string VaiTro { get; set; } = null!;
    public List<string> Quyen { get; set; } = new();
}

public class RoleDto
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public string? MoTa { get; set; }
    public int SoNhanVien { get; set; }
    public List<PermissionDto> Quyen { get; set; } = new();

    /// <summary>Vai trò hệ thống (QUAN_LY, LE_TAN, BUONG_PHONG, KE_TOAN, KHACH_HANG): không đổi tên được.</summary>
    public bool LaVaiTroHeThong { get; set; }
    public int SoQuyenNhayCam { get; set; }

    /// <summary>Tên nhân viên đang giữ vai trò này (tối đa 30).</summary>
    public List<string> NhanVienThuoc { get; set; } = new();

    /// <summary>So với quyền mặc định của vai trò hệ thống: quyền được cấp thêm (có thể là cấp thừa) / quyền mặc định đang thiếu. Rỗng với vai trò tự tạo.</summary>
    public List<string> CapThemSoVoiMacDinh { get; set; } = new();
    public List<string> ThieuSoVoiMacDinh { get; set; } = new();

    /// <summary>Cảnh báo về tách bạch trách nhiệm / cấp thừa quyền (VD vừa thu tiền vừa duyệt không giới hạn).</summary>
    public List<string> CanhBao { get; set; } = new();
}

public class RoleRequest
{
    public string TenVaiTro { get; set; } = null!;
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột VaiTro.MoTa
    public string? MoTa { get; set; }

    /// <summary>Tập quyền mới của vai trò (khi PUT: thay thế toàn bộ tập cũ).</summary>
    public List<int> MaQuyen { get; set; } = new();

    /// <summary>Lý do thay đổi phân quyền (ghi vào nhật ký; màn hình bắt buộc nhập khi thay đổi quyền nhạy cảm).</summary>
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

/// <summary>Kết quả xem trước khi lưu phân quyền: ai bị ảnh hưởng, cấp thêm / thu hồi gì, có nhạy cảm không.</summary>
public class RoleChangePreviewDto
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public bool KhongCoThayDoi { get; set; }
    public int SoNhanVienAnhHuong { get; set; }
    public List<string> NhanVienAnhHuong { get; set; } = new();
    public List<PermissionDto> CapThem { get; set; } = new();
    public List<PermissionDto> ThuHoi { get; set; } = new();
    public List<string> CapThemNhayCam { get; set; } = new();
    public List<string> ThuHoiNhayCam { get; set; } = new();
    public List<string> CanhBao { get; set; } = new();

    /// <summary>Có cấp thêm / thu hồi quyền nhạy cảm: nên (màn hình bắt buộc) ghi lý do.</summary>
    public bool NenCoLyDo { get; set; }

    /// <summary>Thay đổi sẽ bị từ chối (VD QUAN_LY thiếu quyền quản trị); rỗng = hợp lệ.</summary>
    public string? LoiChan { get; set; }
    public string TomTat { get; set; } = "";
}

public class RoleHistoryEntryDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string MoTaHanhDong { get; set; } = "";
    public string? DoiTuong { get; set; }
    public string? ChiTiet { get; set; }
}

/// <summary>Lý do thao tác tài khoản (khóa / mở khóa / đặt lại mật khẩu), ghi vào nhật ký.</summary>
public class AccountActionRequest
{
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class EmployeeDto
{
    public string MaNV { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string ChucVu { get; set; } = null!;
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public string? TenDangNhap { get; set; }
    /// <summary>HOAT_DONG hoặc BI_KHOA (khóa tự động sau 5 lần nhập sai mật khẩu); null nếu nhân viên chưa có tài khoản.</summary>
    public string? TrangThaiTaiKhoan { get; set; }

    /// <summary>Lần đăng nhập thành công gần nhất (null nếu chưa từng đăng nhập).</summary>
    public DateTime? LanDangNhapCuoi { get; set; }

    /// <summary>Số lần nhập sai mật khẩu liên tiếp hiện tại (về 0 khi đăng nhập đúng; đủ 5 lần thì tài khoản tự khóa).</summary>
    public int SoLanDangNhapSai { get; set; }

    /// <summary>Tài khoản đang có phiên đăng nhập còn hiệu lực (còn refresh token chưa hết hạn, chưa đăng xuất).</summary>
    public bool CoPhienDangNhap { get; set; }

    /// <summary>Phiên đăng nhập hiện tại hết hạn lúc nào (null nếu không có phiên).</summary>
    public DateTime? PhienHetHan { get; set; }
}

public class CreateEmployeeRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    [StringLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string ChucVu { get; set; } = null!;
    public int MaVaiTro { get; set; }

    /// <summary>Tên đăng nhập của nhân viên (3-50 ký tự chữ/số/._-, phải có ít nhất một chữ cái, không được là số điện thoại).</summary>
    public string TenDangNhap { get; set; } = null!;

    /// <summary>Mật khẩu tạm; nhân viên nên đổi ngay ở lần đăng nhập đầu tiên.</summary>
    public string MatKhau { get; set; } = null!;
}

public class UpdateEmployeeRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    [StringLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string ChucVu { get; set; } = null!;
}

public class ChangeEmployeeRoleRequest
{
    public int MaVaiTro { get; set; }

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class ChangeEmployeeRoleResultDto
{
    public EmployeeDto NhanVien { get; set; } = null!;
    public string VaiTroCu { get; set; } = null!;
    public string GhiChu { get; set; } = null!;
}

public class ResetEmployeePasswordRequest
{
    /// <summary>Mật khẩu tạm do quản lý đặt; bỏ trống = hệ thống tự sinh mật khẩu ngẫu nhiên mạnh và trả về MỘT lần trong kết quả. Nhân viên nên đổi ngay sau khi đăng nhập.</summary>
    public string? MatKhauMoi { get; set; }

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class EmployeeAccountResultDto
{
    public EmployeeDto NhanVien { get; set; } = null!;
    public string GhiChu { get; set; } = null!;

    /// <summary>Mật khẩu tạm do hệ thống sinh (chỉ có khi đặt lại mật khẩu mà không nhập mật khẩu; chỉ trả về lần này, không lưu dạng đọc được).</summary>
    public string? MatKhauTam { get; set; }
}
