namespace KLCN060.Domain;

/// <summary>
/// Danh mục quyền của hệ thống. Mỗi thao tác của API gắn với ĐÚNG MỘT quyền dưới đây (xem <c>[HasPermission]</c> trên các controller);
/// quyền của từng vai trò lưu ở bảng VaiTro_Quyen và do Quản lý chỉnh ở màn hình D15 — chỉnh xong có hiệu lực ngay ở lần gọi API kế tiếp.
/// </summary>
public static class Permissions
{
    public const string XemPhong = "XEM_PHONG";
    public const string DoiTinhTrangPhong = "DOI_TINH_TRANG_PHONG";
    public const string QuanLyPhong = "QUAN_LY_PHONG";
    public const string QuanLyDichVu = "QUAN_LY_DICH_VU";
    public const string QuanLyKhuyenMai = "QUAN_LY_KHUYEN_MAI";
    public const string XemKhachHang = "XEM_KHACH_HANG";
    public const string XemDatPhong = "XEM_DAT_PHONG";
    public const string ThaoTacLeTan = "THAO_TAC_LE_TAN";
    public const string XemLuuTru = "XEM_LUU_TRU";
    public const string ThanhToan = "THANH_TOAN";
    public const string XemBaoCao = "XEM_BAO_CAO";
    public const string QuanTriHeThong = "QUAN_TRI_HE_THONG";
    public const string DuyetNghiepVu = "DUYET_NGHIEP_VU";
    public const string KhoaMoPhong = "KHOA_MO_PHONG";
    public const string XemNhatKy = "XEM_NHAT_KY";

    public sealed record Info(string TenQuyen, string NhomChucNang, string MoTa);

    /// <summary>Toàn bộ quyền (thứ tự = thứ tự hiển thị ở D15).</summary>
    public static readonly IReadOnlyList<Info> DanhMuc = new List<Info>
    {
        new(XemPhong, "Phong", "Xem sơ đồ phòng và cơ sở vật chất của từng phòng."),
        new(DoiTinhTrangPhong, "Phong", "Đổi tình trạng phòng (đã dọn, chưa dọn, có khách...). Dành cho lễ tân và quản lý; riêng việc khóa / mở phòng bảo trì cần quyền KHOA_MO_PHONG."),
        new(KhoaMoPhong, "Phong", "Đưa phòng vào bảo trì / khóa phòng (OOO, bắt buộc lý do) và mở lại phòng đang bảo trì. Chỉ dành cho quản lý."),
        new(QuanLyPhong, "Phong", "Thêm/sửa/xóa loại phòng, phòng, cơ sở vật chất và ảnh phòng."),
        new(QuanLyDichVu, "DichVu", "Thêm/sửa/xóa danh mục dịch vụ và các mục chi tiết dịch vụ."),
        new(QuanLyKhuyenMai, "KhuyenMai", "Xem, thêm và sửa chương trình khuyến mãi."),
        new(XemKhachHang, "KhachHang", "Tìm, xem và cập nhật hồ sơ khách hàng."),
        new(XemDatPhong, "DatPhong", "Xem danh sách và chi tiết các phiếu đặt phòng."),
        new(ThaoTacLeTan, "LeTan", "Tạo đặt phòng hộ khách, xác nhận cọc tiền mặt, hủy phiếu, nhận/đổi/trả phòng, ghi dịch vụ sử dụng, tạo khách vãng lai."),
        new(XemLuuTru, "LuuTru", "Xem các lượt lưu trú đang ở, dịch vụ đã dùng và bảng tính tiền tạm (folio)."),
        new(ThanhToan, "ThanhToan", "Xem hóa đơn, thu tiền mặt, tạo mã QR chuyển khoản và xuất hóa đơn PDF."),
        new(XemBaoCao, "BaoCao", "Xem báo cáo thống kê (doanh thu, công suất phòng, khách hàng, đặt phòng), công nợ và hóa đơn (chỉ xem)."),
        new(DuyetNghiepVu, "QuanLy", "Quyền của Quản lý trong nghiệp vụ đặt phòng: hủy phiếu (bắt buộc ghi lý do), đánh dấu khách không đến, sửa ngày phiếu đặt trong trường hợp đặc biệt."),
        new(QuanTriHeThong, "HeThong", "Quản lý vai trò, quyền, tài khoản nhân viên (tạo, khóa/mở, đặt lại mật khẩu) và sao lưu/phục hồi dữ liệu."),
        new(XemNhatKy, "HeThong", "Xem nhật ký hệ thống: ai đã làm gì, lúc nào, kết quả ra sao (chỉ xem, không sửa / xóa)."),
    };

    /// <summary>Quyền mặc định của từng vai trò khi tạo mới CSDL (khớp phạm vi công việc thực tế của từng vai trò).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> MacDinhTheoVaiTro = new Dictionary<string, string[]>
    {
        ["QUAN_LY"] = new[] { XemPhong, DoiTinhTrangPhong, KhoaMoPhong, QuanLyPhong, QuanLyDichVu, QuanLyKhuyenMai, XemKhachHang, XemDatPhong, XemLuuTru, XemBaoCao, DuyetNghiepVu, XemNhatKy, QuanTriHeThong },
        ["LE_TAN"] = new[] { XemPhong, DoiTinhTrangPhong, XemKhachHang, XemDatPhong, ThaoTacLeTan, XemLuuTru, ThanhToan },
    };

    public static string? MoTaCua(string tenQuyen) => DanhMuc.FirstOrDefault(x => x.TenQuyen == tenQuyen)?.MoTa;
}
