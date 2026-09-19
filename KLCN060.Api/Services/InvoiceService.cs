using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class InvoiceService : IInvoiceService
{
    private readonly KLCN060DbContext _context;

    public InvoiceService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceDto> GetAsync(string maHD, CurrentUser user)
    {
        var dto = await LayAsync(maHD);
        KiemTraQuyenXem(dto, user);
        return dto;
    }

    public async Task<InvoiceDto> AddPaymentAsync(string maHD, PaymentRequest request, CurrentUser user)
    {
        if (!Enum.TryParse<HinhThucThanhToan>(request.HinhThucThanhToan, ignoreCase: true, out var hinhThuc))
            throw new ApiException(StatusCodes.Status400BadRequest, "HINH_THUC_THANH_TOAN_KHONG_HOP_LE", "HinhThucThanhToan không hợp lệ. Chỉ chấp nhận: TIEN_MAT, CHUYEN_KHOAN, THE.", "hinhThucThanhToan");
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

        var daThu = await _context.ChiTietThanhToans.Where(x => x.MaHD == maHD).SumAsync(x => (decimal?)x.SoTien) ?? 0;
        var conNo = hoaDon.TongTien - daThu;
        if (request.SoTien > conNo)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_VUOT_QUA_CON_NO", $"Số tiền vượt quá số còn nợ ({Math.Round(conNo, 0):N0} VNĐ).", "soTien");

        _context.ChiTietThanhToans.Add(new ChiTietThanhToan
        {
            MaHD = maHD,
            HinhThucThanhToan = hinhThuc,
            SoTien = request.SoTien,
            ThoiGianThanhToan = DateTime.Now,
            MaTaiKhoanThuNgan = user.MaNV,
            MaGiaoDich = request.MaGiaoDich
        });

        // Trạng thái luôn do server tính lại từ tổng các lần thanh toán, không nhận từ client.
        var tongDaThu = daThu + request.SoTien;
        hoaDon.TrangThaiThanhToan = tongDaThu >= hoaDon.TongTien ? TrangThaiThanhToan.DA_THANH_TOAN
            : tongDaThu > 0 ? TrangThaiThanhToan.THANH_TOAN_MOT_PHAN
            : TrangThaiThanhToan.CHUA_THANH_TOAN;
        hoaDon.HinhThucThanhToan = hinhThuc;

        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = user.TenDN,
            HanhDong = "THANH_TOAN_HOA_DON",
            DoiTuongTacDong = maHD,
            ThoiGian = DateTime.Now,
            ChiTiet = $"hinhThuc={hinhThuc}; soTien={Math.Round(request.SoTien, 0)}; maGiaoDich={request.MaGiaoDich}; trangThai={hoaDon.TrangThaiThanhToan}"
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LayAsync(maHD);
    }

    public async Task<(byte[] Content, string FileName)> ExportPdfAsync(string maHD, CurrentUser user)
    {
        var dto = await GetAsync(maHD, user);
        return (InvoicePdfBuilder.Build(dto), $"HoaDon_{maHD}.pdf");
    }

    private async Task<InvoiceDto> LayAsync(string maHD)
    {
        var hoaDon = await _context.HoaDons
            .AsNoTracking()
            .Include(x => x.ChiTietHoaDons)
            .Include(x => x.ChiTietThanhToans)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(d => d!.Khach)
            .FirstOrDefaultAsync(x => x.MaHD == maHD)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");

        var khach = hoaDon.PhieuNhanPhong.PhieuDatPhong?.Khach;
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
