using KLCN060.Domain;

namespace KLCN060.Api.Services;

/// <summary>
/// Danh mục quyền viết bằng ngôn ngữ nghiệp vụ cho màn Phân quyền: tên dễ hiểu, nhóm chức năng, mức nhạy cảm.
/// Người quản trị không phải nhớ mã quyền kỹ thuật (QUAN_LY_GIA...) mà chọn theo việc được phép làm.
/// </summary>
public static class PermissionCatalog
{
    public const string Thuong = "THUONG";
    public const string NhayCam = "NHAY_CAM";
    public const string RatNhayCam = "RAT_NHAY_CAM";

    public sealed record Muc(string TenHienThi, string NhomHienThi, string MucNhayCam);

    private static readonly Dictionary<string, Muc> Bang = new()
    {
        [Permissions.XemPhong] = new("Xem phòng và thiết bị", "Phòng & buồng phòng", Thuong),
        [Permissions.DoiTinhTrangPhong] = new("Đổi tình trạng phòng", "Phòng & buồng phòng", Thuong),
        [Permissions.XacNhanDonPhong] = new("Xác nhận dọn / kiểm tra phòng", "Phòng & buồng phòng", Thuong),
        [Permissions.KhoaMoPhong] = new("Khóa / mở phòng bảo trì", "Phòng & buồng phòng", NhayCam),
        [Permissions.GhiNhanBuongPhong] = new("Ghi nhận buồng phòng (sự cố, minibar, đồ bỏ quên)", "Phòng & buồng phòng", Thuong),
        [Permissions.XuLySuCo] = new("Tiếp nhận và xử lý sự cố phòng", "Phòng & buồng phòng", Thuong),
        [Permissions.QuanLyPhong] = new("Quản lý danh mục phòng và cơ sở vật chất", "Danh mục & giá", NhayCam),
        [Permissions.QuanLyDichVu] = new("Quản lý dịch vụ và gói dịch vụ", "Danh mục & giá", NhayCam),
        [Permissions.QuanLyKhuyenMai] = new("Quản lý khuyến mãi", "Danh mục & giá", NhayCam),
        [Permissions.QuanLyGia] = new("Quản lý bảng giá phòng", "Danh mục & giá", NhayCam),
        [Permissions.XemKhachHang] = new("Xem và cập nhật hồ sơ khách hàng", "Khách hàng & đặt phòng", Thuong),
        [Permissions.XemDatPhong] = new("Xem đặt phòng", "Khách hàng & đặt phòng", Thuong),
        [Permissions.ThaoTacLeTan] = new("Thao tác lễ tân (đặt / nhận / đổi / trả phòng, hủy phiếu)", "Khách hàng & đặt phòng", NhayCam),
        [Permissions.XemLuuTru] = new("Xem lượt lưu trú và bảng tính tiền tạm", "Khách hàng & đặt phòng", Thuong),
        [Permissions.ThanhToan] = new("Thu tiền và xuất hóa đơn", "Tiền & đối soát", NhayCam),
        [Permissions.XemDoiSoat] = new("Xem chuyển khoản / đối soát", "Tiền & đối soát", Thuong),
        [Permissions.XuLyDoiSoat] = new("Xử lý thông báo tiền về bất thường (đối soát chuyển khoản)", "Tiền & đối soát", RatNhayCam),
        [Permissions.XemBaoCao] = new("Xem báo cáo và bảng điều hành", "Báo cáo & nhật ký", Thuong),
        [Permissions.XemNhatKy] = new("Xem toàn bộ nhật ký hoạt động", "Báo cáo & nhật ký", NhayCam),
        [Permissions.XemNhatKyTaiChinh] = new("Xem nhật ký nghiệp vụ tài chính", "Báo cáo & nhật ký", Thuong),
        [Permissions.XuatNhatKy] = new("Xuất nhật ký hoạt động ra file", "Báo cáo & nhật ký", NhayCam),
        [Permissions.DuyetNghiepVu] = new("Quyền Quản lý trong đặt phòng (hủy phiếu, không đến, sửa phiếu đặc biệt)", "Quản lý nghiệp vụ", RatNhayCam),
        [Permissions.QuanTriHeThong] = new("Quản trị hệ thống: phân quyền, tài khoản nhân viên, sao lưu", "Quản trị", RatNhayCam),
    };

    public static Muc Cua(string tenQuyen)
        => Bang.TryGetValue(tenQuyen, out var m) ? m : new(tenQuyen, "Khác", Thuong);

    public static string TenHienThi(string tenQuyen) => Cua(tenQuyen).TenHienThi;
    public static bool LaNhayCam(string tenQuyen) => Cua(tenQuyen).MucNhayCam != Thuong;
    public static bool LaRatNhayCam(string tenQuyen) => Cua(tenQuyen).MucNhayCam == RatNhayCam;
}
