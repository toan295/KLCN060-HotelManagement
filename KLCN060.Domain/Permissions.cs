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
    public const string XemDoiSoat = "XEM_DOI_SOAT";
    public const string XemBaoCao = "XEM_BAO_CAO";
    public const string QuanTriHeThong = "QUAN_TRI_HE_THONG";
    public const string QuanLyGia = "QUAN_LY_GIA";
    public const string DuyetNghiepVu = "DUYET_NGHIEP_VU";
    public const string XemNhatKy = "XEM_NHAT_KY";
    public const string XacNhanDonPhong = "XAC_NHAN_DON_PHONG";
    public const string GhiNhanBuongPhong = "GHI_NHAN_BUONG_PHONG";
    public const string XuLySuCo = "XU_LY_SU_CO";
    public const string KhoaMoPhong = "KHOA_MO_PHONG";
    public const string XuLyDoiSoat = "XU_LY_DOI_SOAT";
    public const string XemNhatKyTaiChinh = "XEM_NHAT_KY_TAI_CHINH";
    public const string XuatNhatKy = "XUAT_NHAT_KY";

    public sealed record Info(string TenQuyen, string NhomChucNang, string MoTa);

    /// <summary>Toàn bộ quyền (thứ tự = thứ tự hiển thị ở D15).</summary>
    public static readonly IReadOnlyList<Info> DanhMuc = new List<Info>
    {
        new(XemPhong, "Phong", "Xem sơ đồ phòng và cơ sở vật chất của từng phòng."),
        new(DoiTinhTrangPhong, "Phong", "Đổi tình trạng phòng (đã dọn, chưa dọn, đã kiểm tra, có khách cần dọn, ngừng phục vụ). Dành cho buồng phòng và quản lý; lễ tân không đổi tình trạng phòng (trả phòng tự đặt phòng thành chưa dọn)."),
        new(XacNhanDonPhong, "Phong", "Xác nhận phòng đã dọn sạch (VD → VC), đã kiểm tra (VC → VI) hoặc dọn xong phòng có khách (OD → OC). Dành cho buồng phòng và quản lý; lễ tân không đổi tình trạng phòng."),
        new(KhoaMoPhong, "Phong", "Đưa phòng vào bảo trì / khóa phòng (OOO, bắt buộc lý do) và mở lại phòng đang bảo trì. Chỉ dành cho quản lý; lễ tân và buồng phòng không tự khóa/mở phòng (buồng phòng báo sự cố để quản lý quyết định)."),
        new(GhiNhanBuongPhong, "BuongPhong", "Báo sự cố trong phòng, kiểm đồ dùng/minibar của phòng, ghi nhận và cập nhật đồ khách bỏ quên (lưu kho). Việc trả đồ cho khách và thanh lý do quản lý xác nhận; lễ tân chỉ tra cứu đồ bỏ quên."),
        new(XuLySuCo, "BuongPhong", "Tiếp nhận, xử lý và đóng các sự cố phòng do nhân viên báo; xem toàn bộ sự cố."),
        new(QuanLyPhong, "Phong", "Thêm/sửa/xóa loại phòng, phòng, cơ sở vật chất và ảnh phòng."),
        new(QuanLyDichVu, "DichVu", "Thêm/sửa/xóa danh mục dịch vụ và các mục chi tiết dịch vụ."),
        new(QuanLyKhuyenMai, "KhuyenMai", "Xem, thêm và sửa chương trình khuyến mãi."),
        new(QuanLyGia, "Gia", "Tạo/sửa/tắt bảng giá phòng: giá cuối tuần, mùa cao điểm, lễ tết, giá đoàn/doanh nghiệp."),
        new(XemKhachHang, "KhachHang", "Tìm, xem và cập nhật hồ sơ khách hàng."),
        new(XemDatPhong, "DatPhong", "Xem danh sách và chi tiết các phiếu đặt phòng."),
        new(ThaoTacLeTan, "LeTan", "Tạo đặt phòng hộ khách, xác nhận cọc tiền mặt, hủy phiếu, nhận/đổi/trả phòng, ghi dịch vụ sử dụng, tạo khách vãng lai."),
        new(XemLuuTru, "LuuTru", "Xem các lượt lưu trú đang ở, dịch vụ đã dùng và bảng tính tiền tạm (folio)."),
        new(ThanhToan, "ThanhToan", "Xem hóa đơn, thu tiền mặt, tạo mã QR chuyển khoản và xuất hóa đơn PDF."),
        new(XemDoiSoat, "ThanhToan", "Xem các yêu cầu chuyển khoản và nhật ký thông báo tiền về từ ngân hàng."),
        new(XuLyDoiSoat, "ThanhToan", "Đối soát chuyển khoản: xử lý thông báo tiền về bất thường (đã xử lý ngoài hệ thống hoặc bỏ qua, có ghi chú)."),
        new(XemBaoCao, "BaoCao", "Xem bảng điều hành, báo cáo (doanh thu, công suất, ADR/RevPAR, vận hành các bộ phận), công nợ, cảnh báo và hóa đơn (chỉ xem)."),
        new(DuyetNghiepVu, "QuanLy", "Quyền của Quản lý trong nghiệp vụ đặt phòng: hủy phiếu (bắt buộc ghi lý do), đánh dấu khách không đến, sửa ngày phiếu đặt trong trường hợp đặc biệt."),
        new(XemNhatKy, "HeThong", "Xem nhật ký hoạt động của toàn hệ thống (đặt phòng, thanh toán, phân phòng, hóa đơn, phân quyền)."),
        new(XemNhatKyTaiChinh, "HeThong", "Chỉ xem nhật ký của các nghiệp vụ tài chính (thanh toán, hóa đơn, đối soát chuyển khoản); không xem nhật ký các bộ phận khác."),
        new(XuatNhatKy, "HeThong", "Xuất nhật ký hoạt động ra file (CSV). Mỗi lần xuất đều được ghi lại: ai xuất, bộ lọc nào, bao nhiêu dòng."),
        new(QuanTriHeThong, "HeThong", "Quản lý vai trò, quyền, tài khoản nhân viên (tạo, khóa/mở, đặt lại mật khẩu) và sao lưu/phục hồi dữ liệu."),
    };

    /// <summary>Quyền mặc định của từng vai trò khi tạo mới CSDL (khớp phạm vi công việc thực tế của từng vai trò).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> MacDinhTheoVaiTro = new Dictionary<string, string[]>
    {
        ["QUAN_LY"] = new[] { XemPhong, DoiTinhTrangPhong, XacNhanDonPhong, KhoaMoPhong, GhiNhanBuongPhong, XuLySuCo, QuanLyPhong, QuanLyDichVu, QuanLyKhuyenMai, QuanLyGia, XemKhachHang, XemDatPhong, XemLuuTru, XemBaoCao, DuyetNghiepVu, XemNhatKy, XuatNhatKy, QuanTriHeThong },
        ["LE_TAN"] = new[] { XemPhong, XemKhachHang, XemDatPhong, ThaoTacLeTan, XemLuuTru, ThanhToan },
        ["KE_TOAN"] = new[] { XemLuuTru, ThanhToan, XemDoiSoat, XuLyDoiSoat, XemBaoCao, XemNhatKyTaiChinh },
        ["BUONG_PHONG"] = new[] { XemPhong, DoiTinhTrangPhong, XacNhanDonPhong, GhiNhanBuongPhong },
    };

    public static string? MoTaCua(string tenQuyen) => DanhMuc.FirstOrDefault(x => x.TenQuyen == tenQuyen)?.MoTa;
}
