using System.Text.RegularExpressions;
using KLCN060.Api.DTOs.Payments;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Payments;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

// Phần 2 của PaymentService: nhận thông báo "tiền về" từ ngân hàng THẬT (webhook của SePay hoặc webhook ký HMAC) và đối soát.
public partial class PaymentService
{
    // Mã giao dịch trong nội dung chuyển khoản, sau khi bỏ khoảng trắng/ký tự đặc biệt và viết hoa: "...GD202609260001..."
    private static readonly Regex MaGiaoDichTrongNoiDung = new(@"GD(\d{8})(\d{4})", RegexOptions.Compiled);

    /// <summary>Webhook ký HMAC-SHA256 (X-Signature): dùng cho cổng/ngân hàng tự ký bằng khóa bí mật dùng chung.</summary>
    public Task<BankNotificationResultDto> HandleBankNotificationAsync(BankNotificationRequest request, string? signature)
    {
        // Chỉ bên biết khóa bí mật mới ký đúng.
        if (!WebhookSignature.Verify(_options.WebhookSecret, request, signature))
        {
            _logger.LogWarning("Webhook ngân hàng bị từ chối: chữ ký không hợp lệ (mã giao dịch ngân hàng: {Ma})", request.MaGiaoDichNganHang);
            throw new ApiException(StatusCodes.Status401Unauthorized, "CHU_KY_KHONG_HOP_LE", "Chữ ký thông báo không hợp lệ.");
        }
        return XuLyWebhookDaXacThucAsync(request);
    }

    /// <summary>
    /// Webhook của SePay (https://docs.sepay.vn): SePay theo dõi biến động số dư tài khoản NGÂN HÀNG THẬT của khách sạn rồi gọi vào đây,
    /// xác thực bằng header "Authorization: Apikey {khóa}". Chỉ giao dịch tiền VÀO mới được đối soát; tiền ra bị bỏ qua.
    /// </summary>
    public async Task<BankNotificationResultDto?> HandleSePayWebhookAsync(SePayWebhookRequest request, string? authorization)
    {
        var apiKey = _options.SePay.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "WEBHOOK_CHUA_CAU_HINH", "Chưa cấu hình Payment:SePay:ApiKey nên chưa nhận được thông báo từ ngân hàng.");

        var gui = authorization?.Trim() ?? string.Empty;
        const string tienTo = "Apikey ";
        var khoa = gui.StartsWith(tienTo, StringComparison.OrdinalIgnoreCase) ? gui[tienTo.Length..].Trim() : string.Empty;
        // So sánh thời gian cố định để không lộ khóa qua độ trễ phản hồi.
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(khoa), System.Text.Encoding.UTF8.GetBytes(apiKey.Trim())))
        {
            _logger.LogWarning("Webhook SePay bị từ chối: sai API key (id giao dịch: {Id})", request.Id);
            throw new ApiException(StatusCodes.Status401Unauthorized, "API_KEY_KHONG_HOP_LE", "API key không hợp lệ.");
        }

        if (!string.Equals(request.TransferType, "in", StringComparison.OrdinalIgnoreCase))
            return null; // tiền ra khỏi tài khoản: không liên quan đến việc thu tiền khách

        if (request.Id <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_ID_GIAO_DICH", "Thiếu id giao dịch.", "id");
        if (!DateTime.TryParseExact(request.TransactionDate, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var thoiGian))
            thoiGian = DateTime.Now;

        // id của SePay là duy nhất theo giao dịch -> làm khóa chống xử lý trùng khi SePay gửi lại (retry).
        return await XuLyWebhookDaXacThucAsync(new BankNotificationRequest
        {
            MaGiaoDichNganHang = $"SEPAY-{request.Id}",
            SoTaiKhoanNhan = (request.AccountNumber ?? string.Empty).Trim(),
            SoTien = request.TransferAmount,
            NoiDung = string.IsNullOrWhiteSpace(request.Content) ? (request.Description ?? string.Empty) : request.Content,
            ThoiGian = thoiGian
        });
    }

    private async Task<BankNotificationResultDto> XuLyWebhookDaXacThucAsync(BankNotificationRequest request)
    {
        KiemTraThongBao(request);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ketQua = await XuLyThongBaoAsync(request);
            await transaction.CommitAsync();
            return ketQua.Dto;
        }
        catch (DbUpdateException)
        {
            // Hai thông báo cùng mã tới cùng lúc: bên kia đã ghi trước -> chỉ trả lại kết quả của bên đó.
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            var maNganHang = request.MaGiaoDichNganHang.Trim();
            var truoc = await _context.ThongBaoNganHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaGiaoDichNganHang == maNganHang);
            if (truoc is not null)
                return KetQuaTuThongBao(truoc, daXuLyTruocDo: true);
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi ghi nhận thông báo ngân hàng, vui lòng thử lại.");
        }
    }

    /// <summary>Trần hợp lý của một giao dịch chuyển khoản (100 tỷ đồng); lớn hơn là dữ liệu sai hoặc tràn cột tiền.</summary>
    private const decimal SoTienToiDaMotGiaoDich = 100_000_000_000m;

    /// <summary>Bước 1 - kiểm tra dữ liệu thông báo (nguồn gửi đã được xác thực ở trên). Chưa đụng tới CSDL.</summary>
    private static void KiemTraThongBao(BankNotificationRequest request)
    {
        if (request.SoTien <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE", "Số tiền phải lớn hơn 0.", "soTien");
        if (request.SoTien > SoTienToiDaMotGiaoDich)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE", $"Số tiền vượt mức tối đa một giao dịch ({SoTienToiDaMotGiaoDich:N0}đ).", "soTien");
        if (request.ThoiGian == default)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_THOI_GIAN", "Thiếu thoiGian của giao dịch ngân hàng.", "thoiGian");
    }

    /// <summary>Kết quả xử lý một thông báo: bản ghi nhật ký, giao dịch khớp (nếu tìm thấy) và DTO trả về.</summary>
    private sealed record KetQuaXuLy(BankNotificationResultDto Dto, ThongBaoNganHang ThongBao, GiaoDichChuyenKhoan? GiaoDich);

    /// <summary>
    /// Lõi xử lý thông báo tiền về. Phải chạy TRONG transaction do người gọi mở và người gọi tự commit/rollback
    /// .
    /// </summary>
    private async Task<KetQuaXuLy> XuLyThongBaoAsync(BankNotificationRequest request)
    {
        var maNganHang = request.MaGiaoDichNganHang.Trim();

        // Bước 2 - CHỐNG XỬ LÝ TRÙNG (idempotency): ngân hàng có thể gửi lại cùng một thông báo (mạng lỗi, retry). Đã ghi nhận rồi thì
        // trả lại đúng kết quả cũ, tuyệt đối không cộng tiền lần nữa.
        var daCo = await _context.ThongBaoNganHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaGiaoDichNganHang == maNganHang);
        if (daCo is not null)
            return new KetQuaXuLy(KetQuaTuThongBao(daCo, daXuLyTruocDo: true), daCo, null);

        var thongBao = new ThongBaoNganHang
        {
            MaGiaoDichNganHang = maNganHang,
            SoTaiKhoanNhan = request.SoTaiKhoanNhan.Trim(),
            SoTien = request.SoTien,
            NoiDung = request.NoiDung.Trim(),
            ThoiGianNganHang = request.ThoiGian,
            ThoiGianNhan = DateTime.Now
        };

        var giaoDich = await DoiSoatAsync(thongBao);
        // Thông báo không khớp (lệch tiền, sai nội dung, muộn, chuyển thừa...) vào hàng chờ xử lý của kế toán.
        thongBao.TrangThaiXuLy = thongBao.KetQua == KetQuaDoiSoatNganHang.KHOP ? TrangThaiXuLyDoiSoat.KHONG_CAN : TrangThaiXuLyDoiSoat.CAN_XU_LY;

        _context.ThongBaoNganHangs.Add(thongBao);
        await _context.SaveChangesAsync();

        return new KetQuaXuLy(KetQuaTuThongBao(thongBao, daXuLyTruocDo: false), thongBao, giaoDich);
    }

    /// <summary>
    /// Đối soát một thông báo tiền về (chưa lưu): điền KetQua/MaGiaoDich/GhiChu, và nếu khớp hoàn toàn thì áp dụng nghiệp vụ
    /// (xác nhận cọc / ghi thanh toán hóa đơn) trong cùng transaction do người gọi mở.
    /// </summary>
    private async Task<GiaoDichChuyenKhoan?> DoiSoatAsync(ThongBaoNganHang thongBao)
    {
        // Bước 3 - Tiền có vào đúng tài khoản của khách sạn không?
        if (!string.Equals(thongBao.SoTaiKhoanNhan, _options.BankHieuLuc().AccountNumber, StringComparison.Ordinal))
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.SAI_TAI_KHOAN_NHAN;
            thongBao.GhiChu = "Tiền chuyển vào tài khoản khác, không phải tài khoản nhận của khách sạn.";
            return null;
        }

        // Bước 4 - Tìm giao dịch theo mã nằm trong nội dung chuyển khoản.
        var maGiaoDich = TrichMaGiaoDich(thongBao.NoiDung);
        if (maGiaoDich is null)
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.KHONG_TIM_THAY_GIAO_DICH;
            thongBao.GhiChu = "Nội dung chuyển khoản không chứa mã giao dịch của hệ thống.";
            return null;
        }

        // Khóa dòng giao dịch: 2 thông báo khác mã cùng trỏ vào một giao dịch (khách chuyển 2 lần) sẽ được xử lý lần lượt, không song song.
        var giaoDich = await _context.GiaoDichChuyenKhoans
            .FromSql($"SELECT * FROM GiaoDichChuyenKhoans WITH (UPDLOCK, HOLDLOCK) WHERE MaGiaoDich = {maGiaoDich}")
            .FirstOrDefaultAsync();
        if (giaoDich is null)
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.KHONG_TIM_THAY_GIAO_DICH;
            thongBao.GhiChu = $"Không có giao dịch nào mang mã {maGiaoDich}.";
            return null;
        }

        thongBao.MaGiaoDich = giaoDich.MaGiaoDich;

        // Bước 5 - Giao dịch còn hiệu lực không?
        if (giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && giaoDich.HanThanhToan <= thongBao.ThoiGianNhan)
            giaoDich.TrangThai = TrangThaiGiaoDichChuyenKhoan.HET_HAN;

        if (giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.THANH_CONG)
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.GIAO_DICH_DA_THANH_TOAN;
            thongBao.GhiChu = "Giao dịch đã được thanh toán trước đó; khoản tiền này là chuyển thừa, cần hoàn lại cho khách.";
            return giaoDich;
        }
        if (giaoDich.TrangThai != TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.GIAO_DICH_KHONG_CON_HIEU_LUC;
            thongBao.GhiChu = $"Giao dịch đã {(giaoDich.TrangThai == TrangThaiGiaoDichChuyenKhoan.HET_HAN ? "hết hạn" : "bị hủy")}; tiền về muộn, cần xử lý/trả lại tiền thủ công.";
            return giaoDich;
        }

        // Bước 6 - ĐỐI CHIẾU SỐ TIỀN: phải đúng từng đồng mới tự động ghi nhận. Lệch (thiếu hoặc thừa) thì không tự xử lý, để nhân viên quyết định.
        if (Math.Round(thongBao.SoTien, 0) != Math.Round(giaoDich.SoTien, 0))
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.SAI_SO_TIEN;
            thongBao.GhiChu = $"Số tiền nhận {thongBao.SoTien:N0} khác số cần thanh toán {giaoDich.SoTien:N0}; chưa ghi nhận, cần xử lý thủ công.";
            return giaoDich;
        }

        // Bước 7 - ÁP DỤNG NGHIỆP VỤ theo loại khoản thanh toán.
        var khongApDung = giaoDich.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC
            ? await ApDungDatCocAsync(giaoDich)
            : await ApDungHoaDonAsync(giaoDich, thongBao);
        if (khongApDung is not null)
        {
            thongBao.KetQua = KetQuaDoiSoatNganHang.KHONG_AP_DUNG_DUOC;
            thongBao.GhiChu = khongApDung;
            return giaoDich;
        }

        giaoDich.TrangThai = TrangThaiGiaoDichChuyenKhoan.THANH_CONG;
        giaoDich.NgayThanhToan = thongBao.ThoiGianNhan;
        giaoDich.MaGiaoDichNganHang = thongBao.MaGiaoDichNganHang;

        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = giaoDich.MaTaiKhoanTao,
            HanhDong = "THANH_TOAN_CHUYEN_KHOAN",
            DoiTuongTacDong = giaoDich.MaThamChieu,
            ThoiGian = DateTime.Now,
            ChiTiet = $"maGiaoDich={giaoDich.MaGiaoDich}; loai={giaoDich.LoaiThamChieu}; soTien={Math.Round(giaoDich.SoTien, 0)}; maNganHang={thongBao.MaGiaoDichNganHang}"
        });

        thongBao.KetQua = KetQuaDoiSoatNganHang.KHOP;
        thongBao.GhiChu = "Đã đối soát khớp và ghi nhận thanh toán.";
        return giaoDich;
    }

    /// <summary>Đặt cọc: phiếu đang CHO_XAC_NHAN -> DA_XAC_NHAN. Trả về lý do nếu không áp dụng được, null nếu thành công.</summary>
    private async Task<string?> ApDungDatCocAsync(GiaoDichChuyenKhoan giaoDich)
    {
        var phieu = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {giaoDich.MaThamChieu}")
            .FirstOrDefaultAsync();
        if (phieu is null)
            return "Không còn phiếu đặt phòng tương ứng.";
        if (phieu.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN)
            return $"Phiếu đặt phòng không còn chờ đặt cọc (hiện tại: {phieu.TrangThai}); tiền về cần được xử lý/hoàn thủ công.";

        phieu.TrangThai = TrangThaiPhieuDat.DA_XAC_NHAN;
        phieu.NgayNhanCoc = DateTime.Now;
        phieu.HinhThucCoc = HinhThucThanhToan.CHUYEN_KHOAN;
        phieu.NguoiNhanCoc = giaoDich.MaTaiKhoanTao;

        // Cọc đã đủ: các mã QR khác còn chờ của phiếu này (khách bấm tạo nhiều lần) không còn ý nghĩa nữa.
        var maHienTai = giaoDich.MaGiaoDich;
        await _context.GiaoDichChuyenKhoans
            .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC && x.MaThamChieu == giaoDich.MaThamChieu
                        && x.MaGiaoDich != maHienTai && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.DA_HUY));
        return null;
    }

    /// <summary>Hóa đơn: ghi 1 lần thanh toán CHUYEN_KHOAN (dùng chung logic với thu tiền tại quầy). Trả về lý do nếu không áp dụng được.</summary>
    private async Task<string?> ApDungHoaDonAsync(GiaoDichChuyenKhoan giaoDich, ThongBaoNganHang thongBao)
    {
        var hoaDon = await _context.HoaDons
            .FromSql($"SELECT * FROM HoaDons WITH (UPDLOCK, HOLDLOCK) WHERE MaHD = {giaoDich.MaThamChieu}")
            .FirstOrDefaultAsync();
        if (hoaDon is null)
            return "Không còn hóa đơn tương ứng.";
        if (string.IsNullOrEmpty(giaoDich.MaNVThuNgan))
            return "Giao dịch không gắn với nhân viên thu ngân.";

        var daThu = await _context.ChiTietThanhToans.Where(x => x.MaHD == hoaDon.MaHD).SumAsync(x => (decimal?)x.SoTien) ?? 0;
        var conNo = hoaDon.TongTien - daThu;
        if (giaoDich.SoTien > conNo)
            return $"Hóa đơn chỉ còn nợ {Math.Round(conNo, 0):N0} (có thể đã được thu bằng cách khác); tiền về cần được xử lý/hoàn thủ công.";

        InvoiceService.GhiNhanThanhToan(_context, hoaDon, daThu, HinhThucThanhToan.CHUYEN_KHOAN, giaoDich.SoTien, giaoDich.MaNVThuNgan, thongBao.MaGiaoDichNganHang);

        // Các mã QR khác còn chờ của hóa đơn này mà số tiền lớn hơn số còn nợ mới thì không còn hợp lệ.
        var conNoMoi = conNo - giaoDich.SoTien;
        var maHienTai = giaoDich.MaGiaoDich;
        await _context.GiaoDichChuyenKhoans
            .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.HOA_DON && x.MaThamChieu == giaoDich.MaThamChieu
                        && x.MaGiaoDich != maHienTai && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.SoTien > conNoMoi)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.DA_HUY));
        return null;
    }

    /// <summary>Tìm mã giao dịch "GDyyyyMMdd-nnnn" trong nội dung chuyển khoản, chịu được việc ngân hàng đổi hoa/thường, bỏ dấu cách hoặc dấu '-'.</summary>
    public static string? TrichMaGiaoDich(string noiDung)
    {
        var chuan = new string(noiDung.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        var m = MaGiaoDichTrongNoiDung.Match(chuan);
        return m.Success ? $"GD{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
    }

    private static BankNotificationResultDto KetQuaTuThongBao(ThongBaoNganHang x, bool daXuLyTruocDo) => new()
    {
        KetQua = x.KetQua.ToString(),
        ThongBao = x.GhiChu ?? string.Empty,
        MaGiaoDich = x.MaGiaoDich,
        MaGiaoDichNganHang = x.MaGiaoDichNganHang,
        DaXuLyTruocDo = daXuLyTruocDo
    };
}
