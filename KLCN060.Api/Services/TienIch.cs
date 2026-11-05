using KLCN060.Api.Middlewares;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Tra họ tên nhân viên theo tên đăng nhập (một truy vấn cho cả danh sách).</summary>
public static class TenNhanVien
{
    public static async Task<Dictionary<string, string?>> TraCuuAsync(KLCN060DbContext context, IEnumerable<string?> tenDNs)
    {
        var ds = tenDNs.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        if (ds.Count == 0) return new Dictionary<string, string?>();
        return await context.TaiKhoans.AsNoTracking().Where(x => ds.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : null })
            .ToDictionaryAsync(x => x.TenDN, x => x.HoTen);
    }
}

/// <summary>Tên người thực hiện để hiển thị cho người xem: "Họ tên (Mã NV)" hoặc "Họ tên (Mã KH)" - không lộ tên đăng nhập.</summary>
public static class TenNguoiDung
{
    public static async Task<Dictionary<string, string>> HienThiAsync(KLCN060DbContext context, IEnumerable<string?> tenDNs)
    {
        var ds = tenDNs.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        if (ds.Count == 0) return new Dictionary<string, string>();
        var rows = await context.TaiKhoans.AsNoTracking().Where(x => ds.Contains(x.TenDN))
            .Select(x => new
            {
                x.TenDN,
                HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null,
                Ma = x.NhanVien != null ? x.NhanVien.MaNV : x.Khach != null ? x.Khach.MaKhach : null
            }).ToListAsync();
        return rows.ToDictionary(x => x.TenDN, x => string.IsNullOrEmpty(x.HoTen) ? x.TenDN : string.IsNullOrEmpty(x.Ma) ? x.HoTen : $"{x.HoTen} ({x.Ma})");
    }

    public static async Task<string?> HienThiAsync(KLCN060DbContext context, string? tenDN)
        => string.IsNullOrEmpty(tenDN) ? tenDN : (await HienThiAsync(context, new[] { tenDN })).GetValueOrDefault(tenDN, tenDN);
}

/// <summary>Đọc giá trị enum từ chuỗi client gửi: chỉ nhận đúng tên (không nhận số), sai thì 400 kèm gợi ý.</summary>
public static class EnumParser
{
    /// <summary>Như <see cref="Parse{T}"/> nhưng không ném lỗi: chỉ nhận đúng tên (không phân biệt hoa thường), không nhận chuỗi số ("2").</summary>
    public static bool TryParse<T>(string? giaTri, out T ketQua) where T : struct, Enum
    {
        ketQua = default;
        var s = giaTri?.Trim() ?? "";
        return s.Length > 0 && !int.TryParse(s, out _) && Enum.TryParse(s, ignoreCase: true, out ketQua) && Enum.IsDefined(ketQua);
    }

    public static T Parse<T>(string? giaTri, string truong, string thongDiep) where T : struct, Enum
    {
        var s = giaTri?.Trim() ?? "";
        if (s.Length == 0 || int.TryParse(s, out _) || !Enum.TryParse<T>(s, ignoreCase: true, out var kq) || !Enum.IsDefined(kq))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", thongDiep, truong);
        return kq;
    }
}

/// <summary>Mô tả tiếng Việt cho mã hành động ghi trong nhật ký thao tác (hiển thị ở lịch sử phiếu đặt phòng, hóa đơn, dịch vụ, thiết bị, vai trò, hồ sơ khách).</summary>
public static class NhatKyMoTa
{
    private static readonly Dictionary<string, string> MoTa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TAO_DAT_PHONG"] = "Tạo phiếu đặt phòng",
        ["TAO_KHACH"] = "Tạo hồ sơ khách hàng",
        ["TAO_PHONG"] = "Thêm phòng vào danh mục",
        ["TAO_PHONG_HANG_LOAT"] = "Thêm nhiều phòng cùng lúc",
        ["SUA_PHONG"] = "Sửa thông tin phòng",
        ["XOA_PHONG"] = "Xóa phòng khỏi danh mục",
        ["TAO_LOAI_PHONG"] = "Tạo loại phòng",
        ["SUA_LOAI_PHONG"] = "Sửa loại phòng",
        ["XOA_LOAI_PHONG"] = "Xóa loại phòng",
        ["TAO_KHUYEN_MAI"] = "Tạo chương trình khuyến mãi",
        ["SUA_KHUYEN_MAI"] = "Sửa chương trình khuyến mãi",
        ["TAM_NGUNG_KHUYEN_MAI"] = "Tạm ngưng khuyến mãi",
        ["TIEP_TUC_KHUYEN_MAI"] = "Tiếp tục áp dụng khuyến mãi",
        ["XOA_KHUYEN_MAI"] = "Xóa chương trình khuyến mãi (chưa dùng)",
        ["CAP_NHAT_KHACH"] = "Cập nhật hồ sơ khách hàng",
        ["XAC_NHAN_COC"] = "Xác nhận đặt cọc tiền mặt",
        ["HUY_DAT_PHONG"] = "Hủy phiếu đặt phòng",
        ["KHONG_DEN"] = "Đánh dấu khách không đến",
        ["NHAN_PHONG"] = "Nhận phòng theo phiếu đặt",
        ["NHAN_PHONG_VANG_LAI"] = "Nhận phòng cho khách vãng lai",
        ["DOI_PHONG"] = "Đổi phòng cho khách đang ở",
        ["GHI_DICH_VU"] = "Ghi dịch vụ khách sử dụng",
        ["TRA_PHONG"] = "Trả phòng",
        ["DOI_TINH_TRANG_PHONG"] = "Đổi tình trạng phòng",
        ["THANH_TOAN_HOA_DON"] = "Thu tiền hóa đơn",
        ["THANH_TOAN_CHUYEN_KHOAN"] = "Ghi nhận thanh toán chuyển khoản",
        ["TAO_VAI_TRO"] = "Tạo vai trò",
        ["CAP_NHAT_PHAN_QUYEN"] = "Cập nhật phân quyền của vai trò",
        ["DOI_VAI_TRO_NHAN_VIEN"] = "Đổi vai trò nhân viên",
        ["TAO_NHAN_VIEN"] = "Tạo nhân viên",
        ["CAP_NHAT_NHAN_VIEN"] = "Cập nhật thông tin nhân viên",
        ["KHOA_TAI_KHOAN_NHAN_VIEN"] = "Khóa tài khoản nhân viên",
        ["MO_KHOA_TAI_KHOAN_NHAN_VIEN"] = "Mở khóa tài khoản nhân viên",
        ["XOA_NHAN_VIEN"] = "Xóa nhân viên (tài khoản đã khóa)",
        ["DAT_LAI_MAT_KHAU_NHAN_VIEN"] = "Đặt lại mật khẩu nhân viên",
        ["TAO_DICH_VU"] = "Thêm dịch vụ",
        ["SUA_DICH_VU"] = "Sửa dịch vụ",
        ["XOA_DICH_VU"] = "Xóa dịch vụ",
        ["DOI_GIA_DICH_VU"] = "Đổi giá dịch vụ",
        ["DOI_NHOM_DOANH_THU_DICH_VU"] = "Đổi nhóm doanh thu của dịch vụ",
        ["DOI_TRANG_THAI_DICH_VU"] = "Đổi trạng thái phục vụ của dịch vụ",
        ["TAO_CHI_TIET_DICH_VU"] = "Thêm mục chi tiết dịch vụ",
        ["SUA_CHI_TIET_DICH_VU"] = "Sửa mục chi tiết dịch vụ",
        ["XOA_CHI_TIET_DICH_VU"] = "Xóa mục chi tiết dịch vụ",
        ["TAO_CO_SO_VAT_CHAT"] = "Thêm thiết bị / cơ sở vật chất",
        ["SUA_CO_SO_VAT_CHAT"] = "Sửa thiết bị / cơ sở vật chất",
        ["DOI_TINH_TRANG_CSVC"] = "Đổi tình trạng thiết bị",
        ["DIEU_CHUYEN_CSVC"] = "Điều chuyển thiết bị",
        ["XOA_CO_SO_VAT_CHAT"] = "Xóa thiết bị / cơ sở vật chất",
        ["HUY_DICH_VU"] = "Hủy dòng dịch vụ ghi nhầm của khách đang ở",
        ["SUA_DAT_PHONG"] = "Sửa phiếu đặt phòng (đổi ngày / phòng / loại phòng)",
        ["NHAN_PHONG_SOM"] = "Nhận phòng sớm (trước ngày đón dự kiến)",
        ["GIA_HAN"] = "Gia hạn lưu trú (dời ngày trả dự kiến)",
        ["THEM_KHACH_DI_CUNG"] = "Thêm người ở cùng vào phiếu đặt phòng",
        ["XOA_KHACH_DI_CUNG"] = "Xóa người ở cùng khỏi phiếu đặt phòng",
        ["DOI_GIA_LOAI_PHONG"] = "Đổi đơn giá cơ bản loại phòng",
        ["DANG_NHAP_THANH_CONG"] = "Đăng nhập",
        ["DANG_NHAP_THAT_BAI"] = "Đăng nhập thất bại",
        ["TAI_KHOAN_BI_KHOA_TU_DONG"] = "Tài khoản bị khóa do nhập sai mật khẩu",
        ["DANG_XUAT"] = "Đăng xuất",
        ["DANG_KY_TAI_KHOAN"] = "Khách đăng ký tài khoản",
        ["SAO_LUU"] = "Sao lưu dữ liệu",
        ["PHUC_HOI"] = "Phục hồi dữ liệu",
        ["CAP_NHAT_CCCD_KHI_NHAN_PHONG"] = "Cập nhật CCCD của khách khi nhận phòng"
    };

    public static string MoTaHanhDong(string hanhDong) => MoTa.TryGetValue(hanhDong, out var mo) ? mo : hanhDong;

    /// <summary>Các mã hành động có mô tả tiếng Việt chứa <paramref name="tuKhoa"/> (không phân biệt hoa thường) - để tìm nhật ký theo mô tả.</summary>
    public static List<string> TimMaTheoMoTa(string tuKhoa)
        => MoTa.Where(x => x.Value.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToList();
}
