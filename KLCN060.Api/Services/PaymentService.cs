using KLCN060.Api.DTOs.Payments;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Payments;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

/// <summary>
/// Thanh toán bằng chuyển khoản vào tài khoản ngân hàng THẬT của khách sạn. Luồng:
///   1. Client gọi CreateTransfer  -> hệ thống tạo GiaoDichChuyenKhoan (CHO_THANH_TOAN) + nội dung chuyển khoản duy nhất + mã QR.
///   2. Khách mở ứng dụng ngân hàng thật, quét mã VietQR (tự điền tài khoản, số tiền, nội dung) và chuyển tiền.
///   3. SePay phát hiện biến động số dư và gọi webhook báo tiền về (HandleSePayWebhookAsync, có API key) -> hệ thống đối soát theo nội dung + số tiền
///      và ghi nhận thanh toán (xác nhận cọc / thu tiền hóa đơn) trong cùng một transaction.
///   4. Client hỏi lại trạng thái (GetTransfer) thấy THANH_CONG.
/// Phần xử lý webhook nằm ở PaymentService.Webhook.cs.
/// </summary>
public partial class PaymentService : IPaymentService
{
    private readonly KLCN060DbContext _context;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(KLCN060DbContext context, IOptions<PaymentOptions> options, ILogger<PaymentService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TransferDto> CreateTransferAsync(CreateTransferRequest request, CurrentUser user)
    {
        if (!EnumParser.TryParse<LoaiThamChieuThanhToan>(request.LoaiThamChieu, out var loai))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_THAM_CHIEU_KHONG_HOP_LE", "loaiThamChieu chỉ chấp nhận: DAT_COC, HOA_DON.", "loaiThamChieu");

        // Chưa khai báo tài khoản ngân hàng thật thì không sinh mã QR: khách có thể chuyển nhầm vào tài khoản mẫu.
        if (!_options.BankDaCauHinh && !_options.DemoMode)
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "NGAN_HANG_CHUA_CAU_HINH",
                "Chưa cấu hình tài khoản ngân hàng nhận tiền của khách sạn (Payment:Bank). Vui lòng liên hệ quản trị viên.");

        var maThamChieu = request.MaThamChieu?.Trim() ?? string.Empty;
        if (maThamChieu.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_THAM_CHIEU", "Thiếu mã phiếu đặt phòng / mã hóa đơn.", "maThamChieu");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Số tiền và quyền luôn do SERVER xác định từ dữ liệu thật, không tin số tiền client gửi lên (trừ chuyển khoản một phần hóa đơn, có kiểm tra trần).
        decimal soTien;
        string? maNVThuNgan = null;
        if (loai == LoaiThamChieuThanhToan.DAT_COC)
        {
            var phieu = await _context.PhieuDatPhongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaPhieuDat == maThamChieu)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");
            if (user.LaKhachHang && user.MaKhach != phieu.MaKhach)
                throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Bạn không có quyền thanh toán cho phiếu đặt phòng này.");
            if (phieu.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN)
                throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE",
                    $"Chỉ thanh toán đặt cọc được khi phiếu đang chờ xác nhận (hiện tại: {phieu.TrangThai}).");
            if (phieu.TienCoc <= 0)
                throw new ApiException(StatusCodes.Status409Conflict, "KHONG_CAN_DAT_COC", "Phiếu đặt phòng này không yêu cầu tiền cọc.");

            soTien = phieu.TienCoc;
        }
        else
        {
            if (user.LaKhachHang || string.IsNullOrEmpty(user.MaNV))
                throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Chỉ nhân viên mới tạo được yêu cầu thanh toán hóa đơn.");

            var hoaDon = await _context.HoaDons.AsNoTracking().FirstOrDefaultAsync(x => x.MaHD == maThamChieu)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");
            var daThu = await _context.ChiTietThanhToans.Where(x => x.MaHD == maThamChieu).SumAsync(x => (decimal?)x.SoTien) ?? 0;
            var conNo = hoaDon.TongTien - daThu;
            if (conNo <= 0)
                throw new ApiException(StatusCodes.Status409Conflict, "HOA_DON_DA_THANH_TOAN", "Hóa đơn này đã được thanh toán đủ.");

            soTien = request.SoTien ?? conNo;
            if (soTien <= 0 || soTien > conNo)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE",
                    $"Số tiền chuyển khoản phải lớn hơn 0 và không vượt số còn nợ ({Math.Round(conNo, 0):N0} VNĐ).", "soTien");

            maNVThuNgan = user.MaNV;
        }

        var bayGio = DateTime.Now;

        // Mỗi khoản (phiếu đặt / hóa đơn) chỉ nên có 1 yêu cầu còn hiệu lực: yêu cầu cũ đã quá hạn -> HET_HAN; nếu khách bấm "tạo mã"
        // nhiều lần với cùng số tiền thì trả lại đúng yêu cầu đang chờ (idempotent), khác số tiền thì hủy yêu cầu cũ.
        var dangCho = await _context.GiaoDichChuyenKhoans
            .Where(x => x.LoaiThamChieu == loai && x.MaThamChieu == maThamChieu && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
            .ToListAsync();

        GiaoDichChuyenKhoan? taiSuDung = null;
        foreach (var cu in dangCho)
        {
            if (cu.HanThanhToan <= bayGio)
                cu.TrangThai = TrangThaiGiaoDichChuyenKhoan.HET_HAN;
            else if (cu.SoTien == soTien && cu.MaTaiKhoanTao == user.TenDN && taiSuDung is null)
                taiSuDung = cu;
            else
                cu.TrangThai = TrangThaiGiaoDichChuyenKhoan.DA_HUY;
        }

        var giaoDich = taiSuDung;
        if (giaoDich is null)
        {
            // Sinh mã tuần tự theo ngày, có khóa để 2 request đồng thời không sinh trùng (cùng cách với mã HD/CI/BK).
            var tienTo = $"GD{bayGio:yyyyMMdd}-";
            var maGiaoDich = await MaCodeGenerator.GenerateNextAsync(_context,
                _context.GiaoDichChuyenKhoans.Where(x => x.MaGiaoDich.StartsWith(tienTo)).Select(x => x.MaGiaoDich), tienTo, 4);

            giaoDich = new GiaoDichChuyenKhoan
            {
                MaGiaoDich = maGiaoDich,
                LoaiThamChieu = loai,
                MaThamChieu = maThamChieu,
                SoTien = soTien,
                // Ngân hàng thường bỏ ký tự đặc biệt trong nội dung nên bỏ dấu '-' của mã: "KLCN060 GD202609260001".
                NoiDungChuyenKhoan = $"KLCN060 {maGiaoDich.Replace("-", string.Empty)}",
                TrangThai = TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN,
                NgayTao = bayGio,
                HanThanhToan = bayGio.AddMinutes(Math.Max(1, _options.TransferExpiryMinutes)),
                MaTaiKhoanTao = user.TenDN,
                MaNVThuNgan = maNVThuNgan
            };
            _context.GiaoDichChuyenKhoans.Add(giaoDich);
        }

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo yêu cầu chuyển khoản, vui lòng thử lại.");
        }

        return ToDto(giaoDich);
    }

    public async Task<TransferDto> GetTransferAsync(string maGiaoDich, CurrentUser user)
    {
        var giaoDich = await TimGiaoDichAsync(maGiaoDich);
        KiemTraQuyenXem(giaoDich, user);
        await CapNhatHetHanAsync(giaoDich);
        return ToDto(giaoDich);
    }

    /// <summary>
    /// CHỈ chế độ thử nghiệm (Payment:DemoMode): giả lập "ngân hàng báo tiền về" cho đúng yêu cầu đang chờ của khách. Thông báo đi qua đúng lõi đối soát như
    /// thông báo thật (kiểm tra số tiền, nội dung, hạn; xác nhận cọc trong cùng transaction) và hiện ở màn Đối soát với mã giao dịch ngân hàng DEMO-...
    /// </summary>
    public async Task<TransferDto> SimulatePaidAsync(string maGiaoDich, CurrentUser user)
    {
        if (!_options.DemoMode)
            throw new ApiException(StatusCodes.Status403Forbidden, "CHE_DO_THU_NGHIEM_TAT", "Chức năng giả lập chuyển khoản chỉ có ở chế độ thử nghiệm.");

        var giaoDich = await TimGiaoDichAsync(maGiaoDich);
        KiemTraQuyenXem(giaoDich, user);
        await CapNhatHetHanAsync(giaoDich);
        if (giaoDich.TrangThai != TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
            throw new ApiException(StatusCodes.Status409Conflict, "GIAO_DICH_KHONG_CON_CHO", $"Giao dịch không còn ở trạng thái chờ thanh toán (hiện tại: {giaoDich.TrangThai}).");

        await XuLyWebhookDaXacThucAsync(new BankNotificationRequest
        {
            MaGiaoDichNganHang = $"DEMO-{giaoDich.MaGiaoDich}",
            SoTaiKhoanNhan = _options.BankHieuLuc().AccountNumber,
            SoTien = giaoDich.SoTien,
            NoiDung = giaoDich.NoiDungChuyenKhoan,
            ThoiGian = DateTime.Now
        });

        _context.ChangeTracker.Clear();
        return ToDto(await TimGiaoDichAsync(maGiaoDich));
    }

    public async Task<TransferDto> CancelTransferAsync(string maGiaoDich, CurrentUser user)
    {
        var giaoDich = await TimGiaoDichAsync(maGiaoDich);
        KiemTraQuyenXem(giaoDich, user);
        await CapNhatHetHanAsync(giaoDich);

        if (giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.THANH_CONG)
            throw new ApiException(StatusCodes.Status409Conflict, "GIAO_DICH_DA_THANH_TOAN", "Giao dịch đã thanh toán thành công, không thể hủy.");

        if (giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
        {
            giaoDich.TrangThai = TrangThaiGiaoDichChuyenKhoan.DA_HUY;
            await _context.SaveChangesAsync();
        }

        return ToDto(giaoDich);
    }

    public async Task<List<TransferDto>> ListPendingForReferenceAsync(string loaiThamChieu, string maThamChieu)
    {
        if (!EnumParser.TryParse<LoaiThamChieuThanhToan>(loaiThamChieu, out var loai))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_THAM_CHIEU_KHONG_HOP_LE", "loaiThamChieu chỉ chấp nhận: HOA_DON, DAT_COC.", "loaiThamChieu");
        var bayGio = DateTime.Now;
        await _context.GiaoDichChuyenKhoans
            .Where(x => x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.HanThanhToan <= bayGio)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.HET_HAN));
        var ds = await _context.GiaoDichChuyenKhoans.AsNoTracking()
            .Where(x => x.LoaiThamChieu == loai && x.MaThamChieu == maThamChieu && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
            .OrderByDescending(x => x.NgayTao).ToListAsync();
        return ds.Select(x => ToDto(x, kemQr: true)).ToList();
    }

    public async Task<(List<TransferDto> Items, int Total)> ListTransfersAsync(TransferListQuery query)
    {
        // Đánh dấu hết hạn hàng loạt trước khi liệt kê để danh sách không hiện "đang chờ" cho giao dịch đã quá hạn.
        var bayGio = DateTime.Now;
        await _context.GiaoDichChuyenKhoans
            .Where(x => x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.HanThanhToan <= bayGio)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.HET_HAN));

        var q = _context.GiaoDichChuyenKhoans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.TrangThai))
        {
            if (!EnumParser.TryParse<TrangThaiGiaoDichChuyenKhoan>(query.TrangThai, out var trangThai))
                throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE", "trangThai chỉ chấp nhận: CHO_THANH_TOAN, THANH_CONG, HET_HAN, DA_HUY.", "trangThai");
            q = q.Where(x => x.TrangThai == trangThai);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.NgayTao).ThenByDescending(x => x.MaGiaoDich)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync();

        // Danh sách không kèm ảnh QR (nặng và không cần).
        return (items.Select(x => ToDto(x, kemQr: false)).ToList(), total);
    }

    public async Task<(List<BankNoticeDto> Items, int Total)> ListBankNoticesAsync(string? ketQua, int page, int pageSize, string? trangThaiXuLy = null)
    {
        var q = _context.ThongBaoNganHangs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(ketQua))
        {
            if (!EnumParser.TryParse<KetQuaDoiSoatNganHang>(ketQua, out var kq))
                throw new ApiException(StatusCodes.Status400BadRequest, "KET_QUA_KHONG_HOP_LE", "ketQua không hợp lệ.", "ketQua");
            q = q.Where(x => x.KetQua == kq);
        }
        if (!string.IsNullOrWhiteSpace(trangThaiXuLy))
        {
            var tt = EnumParser.Parse<TrangThaiXuLyDoiSoat>(trangThaiXuLy, "trangThaiXuLy", "trangThaiXuLy chỉ nhận: KHONG_CAN, CAN_XU_LY, DA_XU_LY.");
            q = q.Where(x => x.TrangThaiXuLy == tt);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await q.CountAsync();
        var items = (await q.OrderByDescending(x => x.ThoiGianNhan).ThenByDescending(x => x.MaThongBao)
            .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync())
            .Select(Finance.ThongBaoMapper.ToDto).ToList();

        return (items, total);
    }

    private async Task<GiaoDichChuyenKhoan> TimGiaoDichAsync(string maGiaoDich)
        => await _context.GiaoDichChuyenKhoans.FirstOrDefaultAsync(x => x.MaGiaoDich == maGiaoDich)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_GIAO_DICH", "Không tìm thấy giao dịch chuyển khoản.");

    // Khách hàng chỉ xem được giao dịch do chính mình tạo; nhân viên (đã được giới hạn theo vai trò ở Controller) xem được tất cả.
    private static void KiemTraQuyenXem(GiaoDichChuyenKhoan giaoDich, CurrentUser user)
    {
        if (user.LaKhachHang && giaoDich.MaTaiKhoanTao != user.TenDN)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Bạn không có quyền xem giao dịch này.");
    }

    /// <summary>Hết hạn "lười": không cần tác vụ nền, mỗi lần đọc/xử lý giao dịch thì kiểm tra hạn và cập nhật trạng thái.</summary>
    private async Task CapNhatHetHanAsync(GiaoDichChuyenKhoan giaoDich)
    {
        if (giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && giaoDich.HanThanhToan <= DateTime.Now)
        {
            giaoDich.TrangThai = TrangThaiGiaoDichChuyenKhoan.HET_HAN;
            await _context.SaveChangesAsync();
        }
    }

    private TransferDto ToDto(GiaoDichChuyenKhoan x, bool kemQr = true)
    {
        var dangCho = x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN;
        var dto = new TransferDto
        {
            MaGiaoDich = x.MaGiaoDich,
            LoaiThamChieu = x.LoaiThamChieu.ToString(),
            MaThamChieu = x.MaThamChieu,
            SoTien = x.SoTien,
            NoiDungChuyenKhoan = x.NoiDungChuyenKhoan,
            TrangThai = x.TrangThai.ToString(),
            NgayTao = x.NgayTao,
            HanThanhToan = x.HanThanhToan,
            NgayThanhToan = x.NgayThanhToan,
            MaGiaoDichNganHang = x.MaGiaoDichNganHang,
            SoGiayConLai = dangCho ? Math.Max(0, (int)Math.Ceiling((x.HanThanhToan - DateTime.Now).TotalSeconds)) : 0,
            NganHang = new BankAccountDto
            {
                TenNganHang = _options.BankHieuLuc().BankName,
                SoTaiKhoan = _options.BankHieuLuc().AccountNumber,
                TenChuTaiKhoan = _options.BankHieuLuc().AccountName
            },
            CheDoThuNghiem = _options.DemoMode,
            DungTaiKhoanMau = _options.DemoMode && !_options.BankDaCauHinh
        };

        if (dangCho && kemQr)
        {
            dto.QrPayload = VietQrBuilder.BuildPayload(_options.BankHieuLuc().BankBin, _options.BankHieuLuc().AccountNumber, x.SoTien, x.NoiDungChuyenKhoan);
            dto.QrPngBase64 = Convert.ToBase64String(VietQrBuilder.RenderPng(dto.QrPayload));
        }

        return dto;
    }
}
