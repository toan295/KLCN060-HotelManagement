using System.Text.RegularExpressions;
using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.EInvoice;

public interface IEInvoiceService
{
    Task<EInvoiceDto?> GetAsync(string maHD);

    /// <summary>Lưu thông tin người mua và phát hành hóa đơn điện tử. Gọi lại với hóa đơn đang CHO_PHAT_HANH/LOI để sửa thông tin và thử lại.</summary>
    Task<EInvoiceDto> IssueAsync(string maHD, IssueEInvoiceRequest request, CurrentUser user);

    /// <summary>Gửi lại hóa đơn đang CHO_PHAT_HANH/LOI với thông tin đã lưu (VD sau khi vừa kết nối nhà cung cấp).</summary>
    Task<EInvoiceDto> RetryAsync(string maHD, CurrentUser user);
}

public class EInvoiceService : IEInvoiceService
{
    private static readonly Regex MaSoThueHopLe = new(@"^\d{10}(-\d{3})?$|^\d{13}$", RegexOptions.Compiled);

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IEInvoiceProvider _provider;
    private readonly EInvoiceOptions _options;
    private readonly ILogger<EInvoiceService> _logger;

    public EInvoiceService(KLCN060DbContext context, IAuditTrail audit, IEInvoiceProvider provider,
        IOptions<EInvoiceOptions> options, ILogger<EInvoiceService> logger)
    {
        _context = context;
        _audit = audit;
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EInvoiceDto?> GetAsync(string maHD)
    {
        var x = await _context.HoaDonDienTus.AsNoTracking().FirstOrDefaultAsync(h => h.MaHD == maHD);
        if (x is null && !await _context.HoaDons.AnyAsync(h => h.MaHD == maHD))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");
        return x is null ? null : ToDto(x);
    }

    public async Task<EInvoiceDto> IssueAsync(string maHD, IssueEInvoiceRequest request, CurrentUser user)
    {
        var ten = request.TenNguoiMua?.Trim() ?? string.Empty;
        var donVi = Chuan(request.TenDonVi);
        var mst = Chuan(request.MaSoThue)?.Replace(" ", string.Empty);
        if (ten.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_NGUOI_MUA", "Vui lòng nhập tên người mua.", "tenNguoiMua");
        // Người mua là tổ chức (có tên đơn vị hoặc MST) thì bắt buộc đủ cả hai để hóa đơn dùng được cho kê khai thuế.
        if ((donVi is null) != (mst is null))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_THONG_TIN_DOANH_NGHIEP", "Hóa đơn xuất cho doanh nghiệp phải có đủ tên đơn vị và mã số thuế.", donVi is null ? "tenDonVi" : "maSoThue");
        if (mst is not null && !MaSoThueHopLe.IsMatch(mst))
            throw new ApiException(StatusCodes.Status400BadRequest, "MA_SO_THUE_KHONG_HOP_LE", "Mã số thuế phải gồm 10 số, 13 số hoặc dạng 10 số-3 số.", "maSoThue");
        var email = Chuan(request.Email);
        if (email is not null) ContactValidator.KiemTraEmail(email, "email");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var hoaDon = await _context.HoaDons
            .FromSql($"SELECT * FROM HoaDons WITH (UPDLOCK, HOLDLOCK) WHERE MaHD = {maHD}")
            .Include(h => h.ChiTietHoaDons).Include(h => h.ChiTietThanhToans)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");

        var daThu = hoaDon.ChiTietThanhToans.Sum(p => p.SoTien);
        if (hoaDon.TongTien - daThu > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "HOA_DON_CHUA_THANH_TOAN_DU",
                $"Hóa đơn còn nợ {Math.Round(hoaDon.TongTien - daThu, 0):N0} VNĐ, chỉ phát hành hóa đơn điện tử khi đã thanh toán đủ.");

        var hddt = await _context.HoaDonDienTus.FirstOrDefaultAsync(h => h.MaHD == maHD);
        if (hddt is { TrangThai: TrangThaiHoaDonDienTu.DA_PHAT_HANH or TrangThaiHoaDonDienTu.DANG_XU_LY })
            throw new ApiException(StatusCodes.Status409Conflict, "HOA_DON_DIEN_TU_DA_PHAT_HANH",
                "Hóa đơn điện tử đã được phát hành/đang xử lý, không thể sửa hoặc phát hành lại. Nếu có sai sót, lập hóa đơn điều chỉnh/thay thế theo quy định.");

        var bayGio = DateTime.Now;
        var (chuaThue, thue, tong) = TinhThue(hoaDon, _options.ThueSuatGTGT);
        var truoc = hddt?.TrangThai;
        if (hddt is null)
        {
            hddt = new HoaDonDienTu { MaHD = maHD, NguoiYeuCau = user.TenDN, NgayTao = bayGio };
            _context.HoaDonDienTus.Add(hddt);
        }
        hddt.TenNguoiMua = ten; hddt.TenDonVi = donVi; hddt.MaSoThue = mst; hddt.DiaChi = Chuan(request.DiaChi); hddt.Email = email;
        hddt.ThueSuat = _options.ThueSuatGTGT; hddt.TienChuaThue = chuaThue; hddt.TienThue = thue; hddt.TongThanhToan = tong;
        hddt.NhaCungCap = _provider.Ten;
        hddt.NgayCapNhat = bayGio;

        await GuiAsync(hddt, hoaDon);

        _audit.Ghi("PHAT_HANH_HOA_DON_DIEN_TU", maHD,
            $"trangThai={truoc?.ToString() ?? "MOI"}->{hddt.TrangThai}; nhaCungCap={hddt.NhaCungCap}; mst={hddt.MaSoThue ?? "khach le"}; tongThanhToan={Math.Round(tong, 0)}; so={hddt.SoHoaDon}; maCQT={hddt.MaCQT}; loi={hddt.ThongBaoLoi}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ToDto(hddt);
    }

    public async Task<EInvoiceDto> RetryAsync(string maHD, CurrentUser user)
    {
        var hddt = await _context.HoaDonDienTus.AsNoTracking().FirstOrDefaultAsync(h => h.MaHD == maHD)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON_DIEN_TU", "Hóa đơn này chưa có hóa đơn điện tử.");
        return await IssueAsync(maHD, new IssueEInvoiceRequest
        {
            TenNguoiMua = hddt.TenNguoiMua, TenDonVi = hddt.TenDonVi, MaSoThue = hddt.MaSoThue, DiaChi = hddt.DiaChi, Email = hddt.Email
        }, user);
    }

    /// <summary>Gửi lên nhà cung cấp và ghi lại kết quả. Lỗi mạng/nhà cung cấp KHÔNG làm mất thông tin người mua: hóa đơn ở trạng thái chờ để gửi lại.</summary>
    private async Task GuiAsync(HoaDonDienTu hddt, HoaDon hoaDon)
    {
        hddt.SoLanThu++;
        hddt.ThongBaoLoi = null;

        if (!_provider.DaCauHinh)
        {
            hddt.TrangThai = TrangThaiHoaDonDienTu.CHO_PHAT_HANH;
            hddt.ThongBaoLoi = "Chưa kết nối nhà cung cấp hóa đơn điện tử nên chưa gửi lên cơ quan thuế. Thông tin người mua đã được lưu; hãy phát hành lại sau khi kết nối.";
            return;
        }

        KetQuaPhatHanhHoaDonDienTu ketQua;
        try
        {
            var dong = hoaDon.ChiTietHoaDons.OrderBy(d => d.MaChiTietHoaDon)
                .Select(d => new DongHoaDonDienTu(d.MoTa, d.SoLuong, d.DonGia, d.ThanhTien)).ToList();
            ketQua = await _provider.PhatHanhAsync(new YeuCauPhatHanhHoaDonDienTu(
                hoaDon.MaHD, hoaDon.NgayLap, hddt.TenNguoiMua, hddt.TenDonVi, hddt.MaSoThue, hddt.DiaChi, hddt.Email,
                hddt.ThueSuat, hddt.TienChuaThue, hddt.TienThue, hddt.TongThanhToan, dong));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogError(ex, "Gọi nhà cung cấp HĐĐT {Provider} thất bại cho hóa đơn {MaHD}.", _provider.Ten, hoaDon.MaHD);
            hddt.TrangThai = TrangThaiHoaDonDienTu.CHO_PHAT_HANH;
            hddt.ThongBaoLoi = "Không kết nối được tới nhà cung cấp hóa đơn điện tử, vui lòng thử lại sau.";
            return;
        }

        hddt.MaThamChieuNhaCungCap = ketQua.MaThamChieuNhaCungCap ?? hddt.MaThamChieuNhaCungCap;
        if (ketQua.ThanhCong)
        {
            hddt.TrangThai = TrangThaiHoaDonDienTu.DA_PHAT_HANH;
            hddt.MauSo = ketQua.MauSo; hddt.KyHieu = ketQua.KyHieu; hddt.SoHoaDon = ketQua.SoHoaDon; hddt.MaCQT = ketQua.MaCQT;
            hddt.NgayPhatHanh = ketQua.NgayPhatHanh ?? DateTime.Now;
            hddt.MaTraCuu = ketQua.MaTraCuu;
            hddt.LienKetTraCuu = ketQua.LienKetTraCuu ?? _options.TrangTraCuu;
        }
        else if (ketQua.DangXuLy)
        {
            hddt.TrangThai = TrangThaiHoaDonDienTu.DANG_XU_LY;
        }
        else
        {
            hddt.TrangThai = TrangThaiHoaDonDienTu.LOI;
            hddt.ThongBaoLoi = ketQua.Loi ?? "Nhà cung cấp từ chối phát hành hóa đơn.";
        }
    }

    /// <summary>Doanh thu hóa đơn = tổng phải thu + tiền đã cọc (cọc là một phần giá trị dịch vụ). Giá đã gồm VAT nên tách ngược ra tiền chưa thuế.</summary>
    internal static (decimal ChuaThue, decimal Thue, decimal Tong) TinhThue(HoaDon hoaDon, decimal thueSuatPhanTram)
    {
        var tong = Math.Round(hoaDon.TongTien + hoaDon.TienDaCoc, 0);
        var chuaThue = Math.Round(tong / (1 + thueSuatPhanTram / 100m), 0);
        return (chuaThue, tong - chuaThue, tong);
    }

    private static string? Chuan(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private EInvoiceDto ToDto(HoaDonDienTu x) => new()
    {
        MaHD = x.MaHD, TrangThai = x.TrangThai.ToString(), TenNguoiMua = x.TenNguoiMua, TenDonVi = x.TenDonVi, MaSoThue = x.MaSoThue,
        DiaChi = x.DiaChi, Email = x.Email, ThueSuat = x.ThueSuat, TienChuaThue = x.TienChuaThue, TienThue = x.TienThue,
        TongThanhToan = x.TongThanhToan, NhaCungCap = x.NhaCungCap, MauSo = x.MauSo, KyHieu = x.KyHieu, SoHoaDon = x.SoHoaDon,
        MaCQT = x.MaCQT, NgayPhatHanh = x.NgayPhatHanh, MaTraCuu = x.MaTraCuu, LienKetTraCuu = x.LienKetTraCuu,
        ThongBaoLoi = x.ThongBaoLoi, SoLanThu = x.SoLanThu, NguoiYeuCau = x.NguoiYeuCau, NgayTao = x.NgayTao, NgayCapNhat = x.NgayCapNhat,
        DaKetNoiNhaCungCap = _provider.DaCauHinh
    };
}
