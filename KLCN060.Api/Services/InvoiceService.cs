using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Finance;
using Microsoft.Extensions.Options;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class InvoiceService : IInvoiceService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly FinanceOptions _finance;

    public InvoiceService(KLCN060DbContext context, IAuditTrail audit, IOptions<FinanceOptions>? finance = null)
    {
        _context = context;
        _audit = audit;
        _finance = finance?.Value ?? new FinanceOptions();
    }

    public async Task<InvoiceDto> GetAsync(string maHD, CurrentUser user)
    {
        var dto = await LayAsync(maHD);
        KiemTraQuyenXem(dto, user);
        return dto;
    }

    public async Task<InvoiceDto> AddPaymentAsync(string maHD, PaymentRequest request, CurrentUser user)
    {
        // IsDefined chặn cả chuỗi số ("2", "7"...) vì Enum.TryParse chấp nhận số không thuộc enum - trước đây "2" từng trùng THE.
        if (!EnumParser.TryParse<HinhThucThanhToan>(request.HinhThucThanhToan, out var hinhThuc))
            throw new ApiException(StatusCodes.Status400BadRequest, "HINH_THUC_THANH_TOAN_KHONG_HOP_LE", "HinhThucThanhToan không hợp lệ. Chỉ chấp nhận: TIEN_MAT (chuyển khoản dùng luồng mã QR).", "hinhThucThanhToan");
        // Chuyển khoản không được "khai báo tay": nhân viên nhập CHUYEN_KHOAN + mã giao dịch tự đặt thì không có gì chứng minh tiền đã về.
        // Phải đi qua luồng chuyển khoản (POST /payments/transfers) để hệ thống tự đối soát với thông báo từ ngân hàng.
        if (hinhThuc == HinhThucThanhToan.CHUYEN_KHOAN)
            throw new ApiException(StatusCodes.Status400BadRequest, "CHUYEN_KHOAN_PHAI_QUA_QR",
                "Thanh toán chuyển khoản phải thực hiện qua mã QR (POST /api/v1/payments/transfers) để hệ thống tự đối soát với ngân hàng.", "hinhThucThanhToan");
        if (request.SoTien <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE", "Số tiền thanh toán phải lớn hơn 0.", "soTien");
        if (string.IsNullOrEmpty(user.MaNV))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Tài khoản không gắn với nhân viên nào.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Khóa hóa đơn: 2 lần thu tiền đồng thời không thể cùng vượt quá số còn nợ.
        var hoaDon = await _context.HoaDons
            .FromSql($"SELECT * FROM HoaDons WITH (UPDLOCK, HOLDLOCK) WHERE MaHD = {maHD}")
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");

        // Chống ghi trùng khi client gửi lại cùng một yêu cầu (bấm đúp, mất mạng rồi thử lại): cùng mã giao dịch trên cùng hóa đơn
        // thì KHÔNG ghi thêm mà trả lại hóa đơn hiện tại (kết quả giống lần đầu). Mã giao dịch bị trùng nhưng khác số tiền là nhầm lẫn -> từ chối.
        if (!string.IsNullOrWhiteSpace(request.MaGiaoDich))
        {
            var daCo = await _context.ChiTietThanhToans.AsNoTracking().FirstOrDefaultAsync(x => x.MaHD == maHD && x.MaGiaoDich == request.MaGiaoDich);
            if (daCo is not null)
            {
                if (daCo.SoTien != request.SoTien)
                    throw new ApiException(StatusCodes.Status409Conflict, "MA_GIAO_DICH_DA_DUNG",
                        $"Mã giao dịch {request.MaGiaoDich} đã được dùng cho khoản {Math.Round(daCo.SoTien, 0):N0}đ khác số tiền lần này.", "maGiaoDich");
                await transaction.CommitAsync();
                return await LayAsync(maHD);
            }
        }

        var daThu = await _context.ChiTietThanhToans.Where(x => x.MaHD == maHD).SumAsync(x => (decimal?)x.SoTien) ?? 0;
        var conNo = hoaDon.TongTien - daThu;
        if (conNo <= 0)
            throw new ApiException(StatusCodes.Status409Conflict, "HOA_DON_DA_THANH_TOAN_DU", "Hóa đơn đã thanh toán đủ, không còn khoản nào để thu (có thể người khác vừa thu xong - tải lại hóa đơn).");
        if (request.SoTien > conNo)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_VUOT_QUA_CON_NO", $"Số tiền vượt quá số còn nợ ({Math.Round(conNo, 0):N0} VNĐ).", "soTien");

        var trangThaiTruoc = hoaDon.TrangThaiThanhToan;
        GhiNhanThanhToan(_context, hoaDon, daThu, hinhThuc, request.SoTien, user.MaNV, request.MaGiaoDich);

        // Sau khi thu thêm tiền tại quầy, mã QR chuyển khoản đang chờ nào có số tiền lớn hơn số còn nợ mới sẽ không còn hợp lệ
        // (khách chuyển sẽ bị thu thừa): hủy chúng để ngân hàng từ chối thay vì trừ tiền khách.
        var conNoMoi = hoaDon.TongTien - (daThu + request.SoTien);
        await _context.GiaoDichChuyenKhoans
            .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.HOA_DON && x.MaThamChieu == maHD
                        && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.SoTien > conNoMoi)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.DA_HUY));

        // Dòng nhật ký chuẩn của hệ thống (có địa chỉ IP, lưu cùng transaction với việc ghi nhận tiền).
        _audit.Ghi("THANH_TOAN_HOA_DON", maHD,
            $"hinhThuc={hinhThuc}; soTien={Math.Round(request.SoTien, 0)}; maGiaoDich={request.MaGiaoDich}; thuNgan={user.TenDN}; conNo={Math.Round(conNo, 0)}->{Math.Round(conNoMoi, 0)}; trangThai={trangThaiTruoc}->{hoaDon.TrangThaiThanhToan}");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LayAsync(maHD);
    }

    /// <summary>
    /// Ghi 1 lần thanh toán vào hóa đơn và tính lại trạng thái thanh toán. Dùng chung cho thu tiền tại quầy (AddPaymentAsync)
    /// và thanh toán chuyển khoản khi ngân hàng báo tiền về (PaymentService) để hai đường đi luôn cho cùng kết quả.
    /// Người gọi phải đã khóa hóa đơn và kiểm tra soTien không vượt số còn nợ; chưa SaveChanges.
    /// </summary>
    internal static void GhiNhanThanhToan(KLCN060DbContext context, HoaDon hoaDon, decimal daThuTruocDo,
        HinhThucThanhToan hinhThuc, decimal soTien, string maNVThuNgan, string? maGiaoDich)
    {
        context.ChiTietThanhToans.Add(new ChiTietThanhToan
        {
            MaHD = hoaDon.MaHD,
            HinhThucThanhToan = hinhThuc,
            SoTien = soTien,
            ThoiGianThanhToan = DateTime.Now,
            MaTaiKhoanThuNgan = maNVThuNgan,
            MaGiaoDich = maGiaoDich
        });

        // Trạng thái luôn do server tính lại từ tổng các lần thanh toán, không nhận từ client.
        var tongDaThu = daThuTruocDo + soTien;
        hoaDon.TrangThaiThanhToan = tongDaThu >= hoaDon.TongTien ? TrangThaiThanhToan.DA_THANH_TOAN
            : tongDaThu > 0 ? TrangThaiThanhToan.THANH_TOAN_MOT_PHAN
            : TrangThaiThanhToan.CHUA_THANH_TOAN;
        hoaDon.HinhThucThanhToan = hinhThuc;
    }

    public async Task<(byte[] Content, string FileName)> ExportPdfAsync(string maHD, CurrentUser user)
    {
        var dto = await GetAsync(maHD, user);
        // [Khối 8 - K8-10] CN04-BR-13: chỉ xuất PDF khi đã thanh toán đủ. Fix P1-1 trước đây chỉ chặn ở nút bấm Desktop
        // (ThanhToanViewModel) nên gọi thẳng Api (hoặc client khác) vẫn phát hành được hóa đơn còn nợ. Quy tắc nghiệp vụ
        // phải nằm ở Api — bộ não nghiệp vụ duy nhất; chặn ở Desktop giữ lại làm lớp phòng thủ thứ 2.
        if (dto.ConNo > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "HOA_DON_CHUA_THANH_TOAN_DU",
                $"Hóa đơn còn nợ {Math.Round(dto.ConNo, 0):N0} VNĐ, chỉ xuất PDF khi đã thanh toán đủ.");
        var hddt = await _context.HoaDonDienTus.AsNoTracking().FirstOrDefaultAsync(x => x.MaHD == maHD);
        return (InvoicePdfBuilder.Build(dto, hddt), TenTepXuat.Tao("HoaDon", "pdf", maHD));
    }

    private async Task<InvoiceDto> LayAsync(string maHD)
    {
        var hoaDon = await _context.HoaDons
            .AsNoTracking()
            .Include(x => x.ChiTietHoaDons)
            .Include(x => x.ChiTietThanhToans)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(d => d!.Khach)
            .FirstOrDefaultAsync(x => x.MaHD == maHD)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");

        // [FIX P1 — 24/09/2026] Ưu tiên PhieuNhanPhong.MaKhach (nguồn trực tiếp, đúng cho cả walk-in lẫn
        // đặt phòng trước — xem KLCN060.Domain.PhieuNhanPhong.MaKhach). Fallback qua PhieuDatPhong.Khach chỉ
        // để tương thích dữ liệu cũ được tạo trước khi có cột MaKhach trực tiếp (nếu có).
        var khach = hoaDon.PhieuNhanPhong.Khach ?? hoaDon.PhieuNhanPhong.PhieuDatPhong?.Khach;
        var daThu = hoaDon.ChiTietThanhToans.Sum(x => x.SoTien);

        return new InvoiceDto
        {
            MaHD = hoaDon.MaHD,
            MaPhieuNhan = hoaDon.MaPhieuNhan,
            MaKhach = khach?.MaKhach,
            HoTenKhach = khach?.HoTen,
            MaNV = hoaDon.MaNV,
            NgayLap = hoaDon.NgayLap,
            TienPhong = hoaDon.TienPhong,
            TienDV = hoaDon.TienDV,
            PhuThu = hoaDon.PhuThu,
            TienDaCoc = hoaDon.TienDaCoc,
            TongTien = hoaDon.TongTien,
            DaThanhToan = daThu,
            ConNo = Math.Max(0, hoaDon.TongTien - daThu),
            HinhThucThanhToan = hoaDon.HinhThucThanhToan.ToString(),
            TrangThaiThanhToan = hoaDon.TrangThaiThanhToan.ToString(),
            ChiTietHoaDon = hoaDon.ChiTietHoaDons.OrderBy(x => x.MaChiTietHoaDon).Select(x => new InvoiceLineDto
            {
                LoaiKhoanMuc = x.LoaiKhoanMuc.ToString(),
                MoTa = x.MoTa,
                SoLuong = x.SoLuong,
                DonGia = x.DonGia,
                ThanhTien = x.ThanhTien
            }).ToList(),
            ChiTietThanhToan = hoaDon.ChiTietThanhToans.OrderBy(x => x.ThoiGianThanhToan).Select(x => new PaymentDto
            {
                MaThanhToan = x.MaThanhToan,
                HinhThucThanhToan = x.HinhThucThanhToan.ToString(),
                SoTien = x.SoTien,
                ThoiGianThanhToan = x.ThoiGianThanhToan,
                MaTaiKhoanThuNgan = x.MaTaiKhoanThuNgan,
                MaGiaoDich = x.MaGiaoDich
            }).ToList()
        };
    }

    private static void KiemTraQuyenXem(InvoiceDto dto, CurrentUser user)
    {
        if (user.LaKhachHang && (dto.MaKhach is null || dto.MaKhach != user.MaKhach))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Bạn không có quyền xem hóa đơn này.");
    }
}
