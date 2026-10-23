using System.Linq.Expressions;
using KLCN060.Api.DTOs.Audit;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IAuditLogService
{
    /// <summary>chiTaiChinh = true: người xem chỉ có quyền xem nhật ký tài chính nên chỉ thấy các dòng thuộc nghiệp vụ tài chính. anChiPhi = true: người xem là Quản lý - dữ liệu chi phí vận hành đã bị loại khỏi vai trò này.</summary>
    Task<(List<AuditLogDto> Items, int TotalItems)> SearchAsync(AuditLogQuery query, bool chiTaiChinh = false);

    /// <summary>Chuỗi sự kiện và lịch sử tình trạng phòng liên quan tới một dòng nhật ký.</summary>
    Task<AuditRelatedDto?> GetRelatedAsync(long maNhatKy, bool chiTaiChinh = false);

    /// <summary>Xuất CSV theo bộ lọc (tối đa 20.000 dòng); việc xuất được ghi vào nhật ký.</summary>
    Task<(byte[] Noi, int SoDong, bool BiCat)> XuatCsvAsync(AuditLogQuery query, string tenDN, bool chiTaiChinh = false, bool xlsx = false);

    /// <summary>Kiểm tra chữ ký từng dòng nhật ký để phát hiện dòng bị sửa trực tiếp trong CSDL.</summary>
    Task<AuditIntegrityDto> KiemTraToanVenAsync(DateOnly? tuNgay = null);

    /// <summary>Lịch sử thay đổi của một đối tượng (mã phiếu đặt, mã hóa đơn, mã phòng...): mọi dòng nhật ký nhắc tới mã đó.</summary>
    Task<List<AuditLogDto>> GetForObjectAsync(string maDoiTuong, int toiDa = 200);
    List<AuditGroupDto> GetGroups(bool chiTaiChinh = false);
}

/// <summary>Xem nhật ký hoạt động toàn hệ thống (Quản lý): lọc theo thời gian, người thực hiện, nhóm nghiệp vụ, từ khóa.</summary>
public partial class AuditLogService : IAuditLogService
{
    private const int PageSizeToiDa = 200;
    private const int MaxTrang = 1_000_000;
    private const int DoiTuongToiDa = 100;
    private const int TuKhoaToiDa = 200;

    // Nhóm nghiệp vụ = tập từ khóa xuất hiện trong tên hành động (cả hành động có tên riêng lẫn dòng chung "POST_/api/v1/...").
    private static readonly (string Ma, string Ten, string[] TuKhoa)[] Nhoms =
    {
        ("DAT_PHONG", "Đặt phòng / nhận phòng / phân phòng", new[] { "/stays", "DAT_PHONG", "XAC_NHAN_COC", "KHONG_DEN", "NHAN_PHONG", "DOI_PHONG", "GIA_HAN", "KHACH_DI_CUNG", "/bookings", "/check-ins", "change-room", "TAO_KHACH", "CAP_NHAT_KHACH", "/guests" }),
        ("THANH_TOAN", "Thanh toán", new[] { "THANH_TOAN", "/payments", "CHUYEN_KHOAN" }),
        ("HOA_DON", "Hóa đơn", new[] { "TRA_PHONG", "/invoices", "check-out", "HOA_DON", "COC_DU" }),
        ("BUONG_PHONG", "Buồng phòng: sự cố / kiểm phòng / đồ bỏ quên", new[] { "SU_CO", "VAT_DUNG", "KIEM_PHONG", "PHIEU_KIEM", "MINIBAR", "DO_BO_QUEN", "NHAN_DON_PHONG", "/incidents", "/room-checks", "/lost-found", "/supplies", "/housekeeping" }),
        ("PHONG", "Phòng / tình trạng phòng", new[] { "TINH_TRANG_PHONG", "/rooms", "/room-types", "/facilities", "GIA_LOAI_PHONG", "CO_SO_VAT_CHAT", "CSVC", "TAO_PHONG", "SUA_PHONG", "XOA_PHONG", "LOAI_PHONG" }),
        ("GIA", "Bảng giá / khuyến mãi", new[] { "QUY_TAC_GIA", "/rates", "KHUYEN_MAI", "/promotions" }),
        ("DICH_VU", "Dịch vụ / gói dịch vụ", new[] { "DICH_VU", "GOI_DICH_VU", "/services", "/service-packages" }),
        ("DOI_SOAT", "Đối soát chuyển khoản / thông báo ngân hàng / xuất báo cáo", new[] { "DOI_SOAT", "THONG_BAO_NGAN_HANG", "XUAT_BAO_CAO", "/reconciliation", "/reports" }),
        ("PHAN_QUYEN", "Phân quyền / vai trò", new[] { "PHAN_QUYEN", "VAI_TRO", "/roles" }),
        ("TAI_KHOAN", "Tài khoản nhân viên", new[] { "NHAN_VIEN", "TAI_KHOAN", "MAT_KHAU", "/employees" }),
        ("CANH_BAO", "Cảnh báo điều hành", new[] { "CANH_BAO", "/alerts" }),
        ("DANG_NHAP", "Đăng nhập / đăng xuất", new[] { "DANG_NHAP", "DANG_XUAT", "DANG_KY" }),
        ("HE_THONG", "Sao lưu / phục hồi / xuất nhật ký", new[] { "SAO_LUU", "PHUC_HOI", "/system", "XUAT_NHAT_KY", "audit-logs" })
    };

    // Nhóm nghiệp vụ tài chính: người chỉ có XEM_NHAT_KY_TAI_CHINH (kế toán) chỉ thấy các dòng thuộc những nhóm này (+ xác nhận cọc tiền mặt, ghi nhận nhóm đặt phòng nhưng là tiền).
    private static readonly string[] NhomTaiChinh = { "THANH_TOAN", "HOA_DON", "DOI_SOAT" };
    private static readonly string[] TuKhoaTaiChinhThem = { "XAC_NHAN_COC" };

    private static string[] TuKhoaTaiChinh() => Nhoms.Where(n => NhomTaiChinh.Contains(n.Ma)).SelectMany(n => n.TuKhoa).Concat(TuKhoaTaiChinhThem).Distinct().ToArray();

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
        ["GIAM_GIA_HOA_DON"] = "Giảm giá hóa đơn (đã được duyệt)",
        ["DIEU_CHINH_THANH_TOAN"] = "Điều chỉnh (đảo) một lần thanh toán (đã được duyệt)",
        ["YEU_CAU_PHE_DUYET_TAO"] = "Gửi yêu cầu phê duyệt",
        ["YEU_CAU_PHE_DUYET_DUYET"] = "Duyệt yêu cầu",
        ["YEU_CAU_PHE_DUYET_TU_CHOI"] = "Từ chối yêu cầu",
        ["YEU_CAU_PHE_DUYET_HUY"] = "Người gửi rút lại yêu cầu",
        ["YEU_CAU_PHE_DUYET_HET_HAN"] = "Yêu cầu phê duyệt hết hạn (hệ thống tự đóng)",
        ["TAO_VAI_TRO"] = "Tạo vai trò",
        ["CAP_NHAT_PHAN_QUYEN"] = "Cập nhật phân quyền của vai trò",
        ["DOI_VAI_TRO_NHAN_VIEN"] = "Đổi vai trò nhân viên",
        ["TAO_NHAN_VIEN"] = "Tạo nhân viên",
        ["CAP_NHAT_NHAN_VIEN"] = "Cập nhật thông tin nhân viên",
        ["KHOA_TAI_KHOAN_NHAN_VIEN"] = "Khóa tài khoản nhân viên",
        ["MO_KHOA_TAI_KHOAN_NHAN_VIEN"] = "Mở khóa tài khoản nhân viên",
        ["XOA_NHAN_VIEN"] = "Xóa nhân viên (tài khoản đã khóa)",
        ["XUAT_BAO_CAO_QUAN_TRI"] = "Xuất báo cáo quản trị",
        ["CAU_HINH_SAO_LUU_TU_DONG"] = "Bật / tắt / đổi chu kỳ sao lưu tự động",
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
        ["DOI_GIA_GOI_DICH_VU"] = "Đổi giá gói dịch vụ",
        ["DOI_TRANG_THAI_GOI_DICH_VU"] = "Đổi trạng thái bán của gói dịch vụ",
        ["TAO_CO_SO_VAT_CHAT"] = "Thêm thiết bị / cơ sở vật chất",
        ["SUA_CO_SO_VAT_CHAT"] = "Sửa thiết bị / cơ sở vật chất",
        ["DOI_TINH_TRANG_CSVC"] = "Đổi tình trạng thiết bị",
        ["DIEU_CHUYEN_CSVC"] = "Điều chuyển thiết bị",
        ["XOA_CO_SO_VAT_CHAT"] = "Xóa thiết bị / cơ sở vật chất",
        ["TAO_GOI_DICH_VU"] = "Tạo gói dịch vụ",
        ["SUA_GOI_DICH_VU"] = "Sửa gói dịch vụ",
        ["XOA_GOI_DICH_VU"] = "Xóa gói dịch vụ",
        ["GHI_GOI_DICH_VU"] = "Ghi gói dịch vụ cho khách đang ở",
        ["HUY_DICH_VU"] = "Hủy dòng dịch vụ ghi nhầm của khách đang ở",
        ["TAO_CHI_PHI"] = "Ghi khoản chi phí",
        ["SUA_CHI_PHI"] = "Sửa khoản chi phí",
        ["XOA_CHI_PHI"] = "Xóa khoản chi phí",
        ["SUA_DAT_PHONG"] = "Sửa phiếu đặt phòng (đổi ngày / phòng / loại phòng)",
        ["NHAN_PHONG_SOM"] = "Nhận phòng sớm (trước ngày đón dự kiến)",
        ["GIA_HAN"] = "Gia hạn lưu trú (dời ngày trả dự kiến)",
        ["THEM_KHACH_DI_CUNG"] = "Thêm người ở cùng vào phiếu đặt phòng",
        ["XOA_KHACH_DI_CUNG"] = "Xóa người ở cùng khỏi phiếu đặt phòng",
        ["BAO_SU_CO"] = "Báo sự cố trong phòng",
        ["DOI_TRANG_THAI_SU_CO"] = "Tiếp nhận / xử lý / đóng sự cố phòng",
        ["THEM_VAT_DUNG"] = "Thêm đồ dùng vào danh mục kiểm phòng",
        ["SUA_VAT_DUNG"] = "Sửa đồ dùng trong danh mục kiểm phòng",
        ["XOA_VAT_DUNG"] = "Xóa đồ dùng khỏi danh mục kiểm phòng",
        ["KIEM_PHONG"] = "Kiểm đồ dùng / minibar của phòng",
        ["HUY_PHIEU_KIEM"] = "Hủy phiếu kiểm phòng nhập nhầm",
        ["TINH_PHI_MINIBAR"] = "Tính phí minibar cho khách (từ kiểm phòng)",
        ["BO_QUA_MINIBAR"] = "Bỏ qua khoản minibar (không tính phí khách)",
        ["GHI_NHAN_DO_BO_QUEN"] = "Ghi nhận đồ khách bỏ quên",
        ["SUA_DO_BO_QUEN"] = "Sửa thông tin đồ bỏ quên",
        ["LUU_KHO_DO_BO_QUEN"] = "Lưu kho đồ bỏ quên",
        ["TRA_DO_BO_QUEN"] = "Trả đồ bỏ quên cho khách",
        ["THANH_LY_DO_BO_QUEN"] = "Thanh lý đồ bỏ quên",
        ["THEM_ANH_DO_BO_QUEN"] = "Thêm ảnh đồ bỏ quên",
        ["XOA_ANH_DO_BO_QUEN"] = "Xóa ảnh đồ bỏ quên",
        ["NHAN_DON_PHONG"] = "Nhận dọn phòng",
        ["BO_NHAN_DON_PHONG"] = "Bỏ nhận dọn phòng",
        ["TAO_QUY_TAC_GIA"] = "Tạo quy tắc giá",
        ["SUA_QUY_TAC_GIA"] = "Sửa quy tắc giá",
        ["XOA_QUY_TAC_GIA"] = "Xóa quy tắc giá",
        ["DOI_GIA_LOAI_PHONG"] = "Đổi đơn giá cơ bản loại phòng",
        ["DANG_NHAP_THANH_CONG"] = "Đăng nhập",
        ["DANG_NHAP_THAT_BAI"] = "Đăng nhập thất bại",
        ["TAI_KHOAN_BI_KHOA_TU_DONG"] = "Tài khoản bị khóa do nhập sai mật khẩu",
        ["DANG_XUAT"] = "Đăng xuất",
        ["DANG_KY_TAI_KHOAN"] = "Khách đăng ký tài khoản",
        ["SAO_LUU"] = "Sao lưu dữ liệu",
        ["KIEM_TRA_SAO_LUU"] = "Kiểm tra bản sao lưu",
        ["PHUC_HOI"] = "Phục hồi dữ liệu",
        ["CHI_HOAN_TIEN"] = "Xác nhận đã chi hoàn tiền cho khách",
        ["COC_DU_CHO_HOAN"] = "Cọc dư cần trả lại khách khi lập hóa đơn",
        ["CAP_NHAT_CCCD_KHI_NHAN_PHONG"] = "Cập nhật CCCD của khách khi nhận phòng",
        ["KIEM_QUY"] = "Lập biên bản kiểm quỹ tiền mặt",
        ["HUY_KIEM_QUY"] = "Hủy biên bản kiểm quỹ",
        ["DOI_SOAT_SAO_KE"] = "Đối chiếu sao kê ngân hàng",
        ["XU_LY_THONG_BAO_NGAN_HANG"] = "Xử lý thông báo tiền về bất thường",
        ["XUAT_BAO_CAO_TAI_CHINH"] = "Xuất báo cáo tài chính",
        ["XUAT_NHAT_KY"] = "Xuất nhật ký hoạt động ra file",
        ["GIAO_CANH_BAO"] = "Giao cảnh báo điều hành cho người xử lý",
        ["GIAO_DON_PHONG"] = "Giao việc dọn phòng cho nhân viên buồng phòng"
    };

    private readonly KLCN060DbContext _context;
    private readonly IConfiguration? _configuration;

    public AuditLogService(KLCN060DbContext context, IConfiguration? configuration = null)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>Phạm vi xem theo quyền: kế toán chỉ thấy nhóm tài chính.</summary>
    private static IQueryable<NhatKyThaoTac> Loc(IQueryable<NhatKyThaoTac> q, bool chiTaiChinh)
    {
        if (chiTaiChinh) q = q.Where(LocTaiChinh());
        return q;
    }

    public List<AuditGroupDto> GetGroups(bool chiTaiChinh = false)
        => Nhoms.Where(x => (!chiTaiChinh || NhomTaiChinh.Contains(x.Ma))).Select(x => new AuditGroupDto { Ma = x.Ma, Ten = x.Ten }).ToList();

    public async Task<(List<AuditLogDto> Items, int TotalItems)> SearchAsync(AuditLogQuery query, bool chiTaiChinh = false)
    {
        // Kẹp số trang: (page-1)*pageSize với page ~ 2 tỷ tràn int → lỗi 500.
        var page = Math.Clamp(query.Page, 1, MaxTrang);
        var pageSize = Math.Clamp(query.PageSize, 1, PageSizeToiDa);
        var q = LocBoLoc(query, chiTaiChinh);

        var total = await q.CountAsync();
        var dong = await q.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync();
        return (await ToDtosAsync(dong), total);
    }

    /// <summary>Áp phạm vi xem + mọi bộ lọc của màn hình (dùng chung cho danh sách và xuất file).</summary>
    private IQueryable<NhatKyThaoTac> LocBoLoc(AuditLogQuery query, bool chiTaiChinh)
    {
        if (query.TuNgay is { } tu && query.DenNgay is { } den && den < tu)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "DenNgay phải sau hoặc bằng TuNgay.", "denNgay");

        var q = _context.NhatKyThaoTacs.AsNoTracking().AsQueryable();
        if (chiTaiChinh && !string.IsNullOrWhiteSpace(query.Nhom) && !NhomTaiChinh.Contains(query.Nhom.Trim().ToUpperInvariant()))
            throw new ApiException(StatusCodes.Status403Forbidden, "NHOM_KHONG_DUOC_XEM", "Bạn chỉ được xem nhật ký của các nghiệp vụ tài chính.", "nhom");
        q = Loc(q, chiTaiChinh);
        if (!string.IsNullOrWhiteSpace(query.KetQua))
        {
            var kq = query.KetQua.Trim().ToUpperInvariant();
            if (kq is not ("THANH_CONG" or "THAT_BAI" or "TU_CHOI"))
                throw new ApiException(StatusCodes.Status400BadRequest, "KET_QUA_KHONG_HOP_LE", "KetQua chỉ chấp nhận: THANH_CONG, THAT_BAI, TU_CHOI.", "ketQua");
            q = q.Where(x => x.KetQua == kq);
        }
        if (!string.IsNullOrWhiteSpace(query.Nguon))
        {
            var nguon = query.Nguon.Trim().ToUpperInvariant();
            if (nguon == "HE_THONG") q = q.Where(x => HanhDongHeThong.Contains(x.HanhDong));
            else if (nguon == "NGUOI_DUNG") q = q.Where(x => !HanhDongHeThong.Contains(x.HanhDong));
            else throw new ApiException(StatusCodes.Status400BadRequest, "NGUON_KHONG_HOP_LE", "Nguon chỉ chấp nhận: NGUOI_DUNG, HE_THONG.", "nguon");
        }
        if (!string.IsNullOrWhiteSpace(query.DoiTuong))
        {
            var dt = query.DoiTuong.Trim();
            // Cột đối tượng tối đa 100 ký tự: chuỗi dài hơn không thể khớp dòng nào (và mẫu LIKE quá dài làm SQL Server báo lỗi).
            if (dt.Length > DoiTuongToiDa) return q.Where(x => false);
            q = q.Where(x => x.DoiTuongTacDong == dt || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.Contains("/" + dt)) || (x.ChiTiet != null && x.ChiTiet.Contains(dt)));
        }
        if (query.TuNgay is { } tuNgay)
        {
            var tuLuc = tuNgay.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.ThoiGian >= tuLuc);
        }
        // DenNgay = ngày lớn nhất (9999-12-31) nghĩa là "không giới hạn trên": cộng thêm 1 ngày sẽ tràn.
        if (query.DenNgay is { } denNgay && denNgay != DateOnly.MaxValue)
        {
            var denLuc = denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.ThoiGian < denLuc);
        }
        if (!string.IsNullOrWhiteSpace(query.TaiKhoan))
        {
            var taiKhoan = query.TaiKhoan.Trim();
            q = q.Where(x => x.MaTaiKhoan == taiKhoan);
        }
        if (!string.IsNullOrWhiteSpace(query.Nhom))
        {
            var nhom = Nhoms.FirstOrDefault(x => string.Equals(x.Ma, query.Nhom.Trim(), StringComparison.OrdinalIgnoreCase));
            if (nhom.Ma is null)
                throw new ApiException(StatusCodes.Status400BadRequest, "NHOM_KHONG_HOP_LE", $"Nhom chỉ chấp nhận: {string.Join(", ", Nhoms.Select(x => x.Ma))}.", "nhom");
            q = q.Where(LocTheoNhom(nhom.Ma));
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            if (tuKhoa.Length > TuKhoaToiDa) return q.Where(x => false);
            q = q.Where(x => x.HanhDong.Contains(tuKhoa) || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.Contains(tuKhoa))
                             || (x.ChiTiet != null && x.ChiTiet.Contains(tuKhoa)));
        }

        return q;
    }

    public async Task<List<AuditLogDto>> GetForObjectAsync(string maDoiTuong, int toiDa = 200)
    {
        if (string.IsNullOrWhiteSpace(maDoiTuong)) return new();
        var ma = maDoiTuong.Trim();
        var dong = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong == ma || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.Contains("/" + ma)))
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy)
            .Take(Math.Clamp(toiDa, 1, 500)).ToListAsync();
        return await ToDtosAsync(dong);
    }

    private async Task<List<AuditLogDto>> ToDtosAsync(List<NhatKyThaoTac> dong)
    {
        var tenDNs = dong.Select(x => x.MaTaiKhoan).Distinct().ToList();
        var nguoi = await _context.TaiKhoans.AsNoTracking().Where(x => tenDNs.Contains(x.TenDN))
            .Select(x => new
            {
                x.TenDN,
                HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null,
                Ma = x.NhanVien != null ? x.NhanVien.MaNV : x.Khach != null ? x.Khach.MaKhach : null,
                VaiTro = x.NhanVien != null ? x.NhanVien.VaiTro.TenVaiTro : "KHACH_HANG"
            }).ToDictionaryAsync(x => x.TenDN);

        return dong.Select(x =>
        {
            nguoi.TryGetValue(x.MaTaiKhoan, out var n);
            var dto = new AuditLogDto
            {
                MaNhatKy = x.MaNhatKy,
                ThoiGian = x.ThoiGian,
                TaiKhoan = x.MaTaiKhoan,
                HoTenNguoiThucHien = n?.HoTen,
                MaNguoiThucHien = n?.Ma,
                VaiTro = n?.VaiTro,
                HanhDong = x.HanhDong,
                MoTaHanhDong = MoTaHanhDong(x.HanhDong),
                Nhom = PhanNhom(x.HanhDong),
                DoiTuongTacDong = x.DoiTuongTacDong,
                ChiTiet = x.ChiTiet,
                KetQua = x.KetQua,
                MaLoi = x.MaLoi,
                NguonThucHien = HanhDongHeThong.Contains(x.HanhDong) ? "HE_THONG" : "NGUOI_DUNG"
            };
            DienChiTiet(dto);
            return dto;
        }).ToList();
    }

    internal static string MoTaHanhDong(string hanhDong)
    {
        if (MoTa.TryGetValue(hanhDong, out var mo)) return mo;
        // Dòng chung do middleware ghi: "POST_/api/v1/bookings/BK1/cancel" -> "Hủy phiếu đặt phòng (BK1)"; không nhận ra thì giữ nguyên để không diễn giải sai.
        return MoTaTuDuongDan(hanhDong);
    }

    internal static string PhanNhom(string hanhDong)
    {
        foreach (var (ma, _, tuKhoa) in Nhoms)
            if (tuKhoa.Any(k => hanhDong.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return ma;
        return "KHAC";
    }

    private static Expression<Func<NhatKyThaoTac, bool>> ChuaMotTrongCacTuKhoa(string[] tuKhoa)
    {
        var x = Expression.Parameter(typeof(NhatKyThaoTac), "x");
        return Expression.Lambda<Func<NhatKyThaoTac, bool>>(HoacCacTuKhoa(x, tuKhoa), x);
    }

    private static Expression HoacCacTuKhoa(ParameterExpression x, IEnumerable<string> tuKhoa)
    {
        var hanhDong = Expression.Property(x, nameof(NhatKyThaoTac.HanhDong));
        var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        Expression? than = null;
        foreach (var k in tuKhoa)
        {
            Expression goi = Expression.Call(hanhDong, contains, Expression.Constant(k));
            than = than is null ? goi : Expression.OrElse(than, goi);
        }
        return than ?? Expression.Constant(false);
    }

    /// <summary>Dòng THUỘC nhóm i theo đúng cách <see cref="PhanNhom"/> hiển thị: có từ khóa của nhóm i và KHÔNG có từ khóa của nhóm đứng trước (khớp đầu tiên thắng).</summary>
    private static Expression ThuocNhom(ParameterExpression x, int viTri)
        => Expression.AndAlso(HoacCacTuKhoa(x, Nhoms[viTri].TuKhoa),
            Expression.Not(HoacCacTuKhoa(x, Nhoms.Take(viTri).SelectMany(n => n.TuKhoa))));

    private static Expression<Func<NhatKyThaoTac, bool>> LocTheoNhom(string ma)
    {
        var x = Expression.Parameter(typeof(NhatKyThaoTac), "x");
        var vt = Array.FindIndex(Nhoms, n => n.Ma == ma);
        return Expression.Lambda<Func<NhatKyThaoTac, bool>>(ThuocNhom(x, vt), x);
    }

    /// <summary>Dòng tài chính = hiển thị thuộc một nhóm tài chính (đúng như cột "Nhóm") hoặc là xác nhận cọc.</summary>
    private static Expression<Func<NhatKyThaoTac, bool>> LocTaiChinh()
    {
        var x = Expression.Parameter(typeof(NhatKyThaoTac), "x");
        Expression than = HoacCacTuKhoa(x, TuKhoaTaiChinhThem);
        for (var i = 0; i < Nhoms.Length; i++)
            if (NhomTaiChinh.Contains(Nhoms[i].Ma)) than = Expression.OrElse(than, ThuocNhom(x, i));
        return Expression.Lambda<Func<NhatKyThaoTac, bool>>(than, x);
    }
}
