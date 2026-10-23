using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

/// <summary>Ngưỡng của các cảnh báo (mục "Alerts" trong appsettings; bỏ trống thì dùng giá trị mặc định dưới đây).</summary>
public class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>Phòng bảo trì (OOO) quá số ngày này thì cảnh báo.</summary>
    public int MaintenanceDays { get; set; } = 3;

    /// <summary>Phòng trống chưa dọn (VD) quá số giờ này thì cảnh báo.</summary>
    public int DirtyRoomHours { get; set; } = 4;

    /// <summary>Sự cố phòng mới báo mà quá số giờ này vẫn chưa ai tiếp nhận thì cảnh báo.</summary>
    public int IncidentAckHours { get; set; } = 4;

    /// <summary>Phiếu đặt sắp đến được coi là VIP khi tổng tiền từ mức này trở lên hoặc đặt từ VipRooms phòng.</summary>
    public decimal VipBookingValue { get; set; } = 10_000_000m;
    public int VipRooms { get; set; } = 3;
    public int VipLookAheadDays { get; set; } = 7;

    /// <summary>Hoàn cọc từ mức này trở lên (trong 7 ngày gần nhất) là "hoàn tiền lớn".</summary>
    public decimal LargeRefund { get; set; } = 3_000_000m;

    /// <summary>Hóa đơn còn nợ quá số ngày này là công nợ quá hạn.</summary>
    public int DebtDays { get; set; } = 7;

    /// <summary>Tỷ lệ hủy 7 ngày gần nhất tăng từ ngưỡng điểm % này so với 7 ngày trước đó (và có ít nhất CancelMinCount lượt hủy) thì cảnh báo.</summary>
    public decimal CancelRateIncreasePoints { get; set; } = 10m;
    public int CancelMinCount { get; set; } = 3;

    /// <summary>Chưa có bản sao lưu thành công nào trong số ngày này thì cảnh báo.</summary>
    public int BackupStaleDays { get; set; } = 7;
}

public interface IAlertService
{
    Task<AlertReportDto> GetAlertsAsync();

    /// <summary>Giao một cảnh báo cho người xử lý + đặt hạn (có ghi nhật ký).</summary>
    Task<AlertDto> AssignAsync(string ma, AssignAlertRequest request, CurrentUser user);
}

/// <summary>
/// Cảnh báo cho Quản lý, tính trực tiếp từ dữ liệu ở thời điểm gọi (không lưu bảng riêng nên luôn đúng với thực tế, không "báo ma" khi sự việc đã được xử lý).
/// Mỗi quy tắc trả về tối đa một cảnh báo kèm danh sách đối tượng cụ thể (tối đa 10).
/// </summary>
public partial class AlertService : IAlertService
{
    private const int ToiDaChiTiet = 10;

    private static readonly TrangThaiPhieuDat[] TrangThaiGiuPhong = { TrangThaiPhieuDat.CHO_XAC_NHAN, TrangThaiPhieuDat.DA_XAC_NHAN };

    private readonly KLCN060DbContext _context;
    private readonly AlertOptions _options;
    private readonly IAuditTrail? _audit;

    public AlertService(KLCN060DbContext context, IOptions<AlertOptions> options, IAuditTrail? audit = null)
    {
        _context = context;
        _options = options.Value;
        _audit = audit;
    }

    private static string Vnd(decimal x) => $"{Math.Round(x, 0):N0}đ";

    public async Task<AlertReportDto> GetAlertsAsync()
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var canhBaos = new List<AlertDto>();

        void Them(string ma, string mucDo, string tieuDe, string moTa, string manHinh, IReadOnlyCollection<string> chiTiet)
        {
            if (chiTiet.Count == 0) return;
            canhBaos.Add(new AlertDto
            {
                Ma = ma, MucDo = mucDo, TieuDe = tieuDe, MoTa = moTa, ManHinh = manHinh,
                SoLuong = chiTiet.Count, ChiTiet = chiTiet.Take(ToiDaChiTiet).ToList()
            });
        }

        // ---------------------------------------------------------------- Phòng: bảo trì quá lâu / chưa dọn
        var phongs = await _context.Phongs.AsNoTracking()
            .Where(x => x.TinhTrang == TinhTrangPhong.OOO || x.TinhTrang == TinhTrangPhong.VD)
            .Select(x => new
            {
                x.MaPhong, x.TinhTrang,
                Cuoi = _context.LichSuTinhTrangPhongs.Where(h => h.MaPhong == x.MaPhong && h.DenTinhTrang == x.TinhTrang)
                    .OrderByDescending(h => h.ThoiGian).ThenByDescending(h => h.MaLichSu)
                    .Select(h => new { h.ThoiGian, h.LyDo }).FirstOrDefault()
            }).ToListAsync();

        Them("BAO_TRI_QUA_LAU", "NGHIEM_TRONG", "Phòng bảo trì quá lâu",
            $"Phòng ngừng phục vụ (OOO) quá {_options.MaintenanceDays} ngày - cần đẩy nhanh sửa chữa hoặc xác nhận lại kế hoạch.", "PHONG",
            phongs.Where(x => x.TinhTrang == TinhTrangPhong.OOO && x.Cuoi is not null && bayGio - x.Cuoi.ThoiGian > TimeSpan.FromDays(_options.MaintenanceDays))
                .OrderBy(x => x.Cuoi!.ThoiGian)
                .Select(x => $"{x.MaPhong}: {(int)(bayGio - x.Cuoi!.ThoiGian).TotalDays} ngày{(string.IsNullOrWhiteSpace(x.Cuoi.LyDo) ? "" : $" - {x.Cuoi.LyDo}")}").ToList());

        Them("PHONG_CHUA_DON", "CANH_BAO", "Phòng trống chưa được dọn",
            $"Phòng trống ở trạng thái chưa dọn (VD) quá {_options.DirtyRoomHours} giờ - ảnh hưởng khả năng nhận khách.", "PHONG",
            phongs.Where(x => x.TinhTrang == TinhTrangPhong.VD && x.Cuoi is not null && bayGio - x.Cuoi.ThoiGian > TimeSpan.FromHours(_options.DirtyRoomHours))
                .OrderBy(x => x.Cuoi!.ThoiGian)
                .Select(x => $"{x.MaPhong}: {Math.Round((bayGio - x.Cuoi!.ThoiGian).TotalHours, 1)} giờ").ToList());

        // ---------------------------------------------------------------- Buồng phòng: sự cố phòng, đồ bỏ quên
        var suCoMo = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiSuCo.MOI || x.TrangThai == TrangThaiSuCo.DA_TIEP_NHAN || x.TrangThai == TrangThaiSuCo.DANG_XU_LY)
            .Select(x => new { x.MaSuCo, x.MaPhong, x.TieuDe, x.MucDo, x.TrangThai, x.ThoiGianBao }).ToListAsync();
        var suCoCao = suCoMo.Where(x => x.MucDo is MucDoSuCo.CAO or MucDoSuCo.KHAN_CAP).OrderByDescending(x => x.MucDo).ThenBy(x => x.ThoiGianBao).ToList();
        Them("SU_CO_MUC_CAO", suCoCao.Any(x => x.MucDo == MucDoSuCo.KHAN_CAP) ? "NGHIEM_TRONG" : "CANH_BAO", "Sự cố phòng mức cao chưa xử lý xong",
            "Sự cố mức Cao/Khẩn cấp do buồng phòng, lễ tân báo vẫn chưa được xử lý xong - có thể ảnh hưởng khách và việc bán phòng.", "BUONG_PHONG",
            suCoCao.Select(x => $"SC{x.MaSuCo:0000} phòng {x.MaPhong} ({(x.MucDo == MucDoSuCo.KHAN_CAP ? "khẩn cấp" : "cao")}): {x.TieuDe} - {(x.TrangThai == TrangThaiSuCo.MOI ? "chưa tiếp nhận" : "đang xử lý")}, {Math.Round((bayGio - x.ThoiGianBao).TotalHours, 1)} giờ").ToList());
        Them("SU_CO_CHUA_TIEP_NHAN", "CANH_BAO", "Sự cố phòng chưa được tiếp nhận",
            $"Sự cố mới báo quá {_options.IncidentAckHours} giờ mà chưa ai tiếp nhận.", "BUONG_PHONG",
            suCoMo.Where(x => x.TrangThai == TrangThaiSuCo.MOI && bayGio - x.ThoiGianBao > TimeSpan.FromHours(_options.IncidentAckHours) && x.MucDo is MucDoSuCo.THAP or MucDoSuCo.TRUNG_BINH)
                .OrderBy(x => x.ThoiGianBao).Select(x => $"SC{x.MaSuCo:0000} phòng {x.MaPhong}: {x.TieuDe}, {Math.Round((bayGio - x.ThoiGianBao).TotalHours, 1)} giờ").ToList());

        var hanThanhLy = bayGio.AddDays(-LostFoundService.SoNgayLuuGiuToiThieu);
        var doDenHan = await _context.DoBoQuens.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU && x.ThoiGianPhatHien <= hanThanhLy)
            .OrderBy(x => x.ThoiGianPhatHien).Select(x => new { x.MaDo, x.MoTa, x.ThoiGianPhatHien }).ToListAsync();
        Them("DO_BO_QUEN_DEN_HAN", "THONG_TIN", "Đồ bỏ quên đã lưu giữ đủ hạn",
            $"Đồ lưu giữ từ {LostFoundService.SoNgayLuuGiuToiThieu} ngày trở lên chưa ai nhận - có thể liên hệ lại khách hoặc thanh lý.", "BUONG_PHONG",
            doDenHan.Select(x => $"DQ{x.MaDo:0000}: {(x.MoTa.Length > 40 ? x.MoTa[..40] + "..." : x.MoTa)} ({(int)(bayGio - x.ThoiGianPhatHien).TotalDays} ngày)").ToList());

        // ---------------------------------------------------------------- Đặt phòng: VIP, quá hạn không đến, trả phòng quá hạn
        var denNgay = homNay.AddDays(_options.VipLookAheadDays);
        var sapDen = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => TrangThaiGiuPhong.Contains(x.TrangThai) && x.NgayDonDuKien >= homNay && x.NgayDonDuKien <= denNgay)
            .Select(x => new { x.MaPhieuDat, x.NgayDonDuKien, HoTen = x.Khach.HoTen, SoPhong = x.ChiTietPhieuDats.Count, Tong = x.ChiTietPhieuDats.Sum(c => (decimal?)c.ThanhTien) ?? 0 })
            .ToListAsync();
        Them("BOOKING_VIP", "THONG_TIN", "Booking VIP sắp đến",
            $"Phiếu đặt trong {_options.VipLookAheadDays} ngày tới có giá trị từ {Vnd(_options.VipBookingValue)} hoặc từ {_options.VipRooms} phòng - nên chuẩn bị phòng và đón tiếp chu đáo.", "DAT_PHONG",
            sapDen.Where(x => x.Tong >= _options.VipBookingValue || x.SoPhong >= _options.VipRooms).OrderBy(x => x.NgayDonDuKien)
                .Select(x => $"{x.MaPhieuDat} ({x.HoTen}): {x.SoPhong} phòng, {Vnd(x.Tong)}, đến {x.NgayDonDuKien:dd/MM}").ToList());

        var quaHan = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => TrangThaiGiuPhong.Contains(x.TrangThai) && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null)
            .OrderBy(x => x.NgayDonDuKien).Select(x => new { x.MaPhieuDat, x.NgayDonDuKien, HoTen = x.Khach.HoTen }).ToListAsync();
        Them("KHONG_DEN_CHO_XU_LY", "CANH_BAO", "Phiếu quá ngày đón chưa nhận phòng",
            "Phiếu vẫn đang giữ phòng dù đã qua ngày đón dự kiến - đánh dấu \"không đến\" để trả phòng cho khách khác.", "DAT_PHONG",
            quaHan.Select(x => $"{x.MaPhieuDat} ({x.HoTen}): quá hạn {homNay.DayNumber - x.NgayDonDuKien.DayNumber} ngày").ToList());

        var traTreTho = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => new { x.MaPhong, Tra = x.NgayTraDuKien ?? (x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.NgayTraDuKien : (DateOnly?)null) })
            .ToListAsync();
        var traTre = traTreTho.Where(x => x.Tra is { } t && t < homNay).Select(x => new { x.MaPhong, NgayTraDuKien = x.Tra!.Value }).ToList();
        Them("TRA_PHONG_QUA_HAN", "CANH_BAO", "Khách ở quá ngày trả dự kiến",
            "Khách vẫn đang ở dù đã quá ngày trả dự kiến - kiểm tra gia hạn hoặc làm thủ tục trả phòng; có thể trùng lịch đặt phòng kế tiếp.", "LUU_TRU",
            traTre.Select(x => $"{x.MaPhong}: quá {homNay.DayNumber - x.NgayTraDuKien.DayNumber} ngày (dự kiến trả {x.NgayTraDuKien:dd/MM})").ToList());

        // ---------------------------------------------------------------- Overbooking / trùng phòng
        await ThemCanhBaoTrungPhongAsync(canhBaos, homNay);

        // ---------------------------------------------------------------- Hủy: hoàn tiền lớn, tỷ lệ hủy tăng
        var bay7 = bayGio.AddDays(-7);
        var hoanLon = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY && x.NgayHuy >= bay7 && x.SoTienHoanCoc >= _options.LargeRefund)
            .OrderByDescending(x => x.SoTienHoanCoc).Select(x => new { x.MaPhieuDat, x.SoTienHoanCoc, x.HuyBoi }).ToListAsync();
        var tenNguoiHoan = await TenNguoiDung.HienThiAsync(_context, hoanLon.Select(x => x.HuyBoi));
        Them("HOAN_COC_LON", "CANH_BAO", "Hoàn cọc giá trị lớn",
            $"Các lần hoàn cọc từ {Vnd(_options.LargeRefund)} trong 7 ngày qua - nên đối chiếu lý do và người duyệt.", "DAT_PHONG",
            hoanLon.Select(x => $"{x.MaPhieuDat}: hoàn {Vnd(x.SoTienHoanCoc)} (xử lý bởi {(x.HuyBoi is null ? "?" : tenNguoiHoan.GetValueOrDefault(x.HuyBoi, x.HuyBoi))})").ToList());

        var bay14 = bayGio.AddDays(-14);
        var huyNay = await _context.PhieuDatPhongs.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY && x.NgayHuy >= bay7);
        var taoNay = await _context.PhieuDatPhongs.CountAsync(x => x.NgayDat >= bay7);
        var huyTruoc = await _context.PhieuDatPhongs.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY && x.NgayHuy >= bay14 && x.NgayHuy < bay7);
        var taoTruoc = await _context.PhieuDatPhongs.CountAsync(x => x.NgayDat >= bay14 && x.NgayDat < bay7);
        var tyLeNay = taoNay == 0 ? 0m : huyNay * 100m / taoNay;
        var tyLeTruoc = taoTruoc == 0 ? 0m : huyTruoc * 100m / taoTruoc;
        if (huyNay >= _options.CancelMinCount && tyLeNay - tyLeTruoc >= _options.CancelRateIncreasePoints)
            Them("TY_LE_HUY_TANG", "CANH_BAO", "Tỷ lệ hủy phòng tăng",
                "Tỷ lệ hủy 7 ngày gần nhất tăng rõ rệt so với 7 ngày trước đó - xem lại nguồn đặt, chính sách hoặc giá.", "DAT_PHONG",
                new[] { $"7 ngày qua: {huyNay} hủy / {taoNay} phiếu tạo = {tyLeNay:0.#}%", $"7 ngày trước đó: {huyTruoc} hủy / {taoTruoc} phiếu tạo = {tyLeTruoc:0.#}%" });

        // ---------------------------------------------------------------- Thanh toán / đối soát / công nợ
        var bay2Ngay = bayGio.AddDays(-2);
        // Chỉ báo những thông báo CHƯA được kế toán xử lý (xử lý xong là hết cảnh báo, không "báo ma"), kể cả khi đã quá 7 ngày.
        var loiDoiSoat = await _context.ThongBaoNganHangs.AsNoTracking()
            .Where(x => x.KetQua != KetQuaDoiSoatNganHang.KHOP && x.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
            .OrderByDescending(x => x.ThoiGianNhan).Select(x => new { x.MaGiaoDichNganHang, x.SoTien, x.KetQua }).ToListAsync();
        Them("DOI_SOAT_LOI", "NGHIEM_TRONG", "Tiền về nhưng không đối soát được",
            "Ngân hàng báo có tiền chuyển vào nhưng hệ thống không ghi nhận được thanh toán (sai số tiền, sai nội dung, giao dịch hết hạn...) - kế toán cần xử lý (ghi nhận đã xử lý hoặc bỏ qua).", "DOI_SOAT",
            loiDoiSoat.Select(x => $"{x.MaGiaoDichNganHang}: {Vnd(x.SoTien)} - {x.KetQua}").ToList());

        var hetHan = await _context.GiaoDichChuyenKhoans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiGiaoDichChuyenKhoan.HET_HAN && x.HanThanhToan >= bay2Ngay)
            .OrderByDescending(x => x.HanThanhToan).Select(x => new { x.MaGiaoDich, x.LoaiThamChieu, x.MaThamChieu, x.SoTien }).ToListAsync();
        Them("CHUYEN_KHOAN_HET_HAN", "THONG_TIN", "Thanh toán chuyển khoản hết hạn",
            "Khách tạo mã QR nhưng không chuyển tiền trước hạn trong 48 giờ qua - phiếu đặt/hóa đơn tương ứng vẫn chưa được thanh toán.", "DOI_SOAT",
            hetHan.Select(x => $"{x.MaGiaoDich}: {x.LoaiThamChieu} {x.MaThamChieu} - {Vnd(x.SoTien)}").ToList());

        var hanNo = bayGio.AddDays(-_options.DebtDays);
        var no = await _context.HoaDons.AsNoTracking()
            .Where(x => x.NgayLap < hanNo)
            .Select(x => new { x.MaHD, x.NgayLap, x.TongTien, DaThu = x.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0 }).ToListAsync();
        var noQuaHan = no.Where(x => x.TongTien - x.DaThu > 0).OrderBy(x => x.NgayLap).ToList();
        Them("CONG_NO_QUA_HAN", noQuaHan.Any(x => bayGio - x.NgayLap > TimeSpan.FromDays(30)) ? "NGHIEM_TRONG" : "CANH_BAO", "Công nợ quá hạn",
            $"Hóa đơn còn nợ quá {_options.DebtDays} ngày kể từ ngày lập - tổng {Vnd(noQuaHan.Sum(x => x.TongTien - x.DaThu))}.", "HOA_DON",
            noQuaHan.Select(x => $"{x.MaHD}: còn nợ {Vnd(x.TongTien - x.DaThu)}, {(int)(bayGio - x.NgayLap).TotalDays} ngày").ToList());

        // ---------------------------------------------------------------- Tài khoản, sao lưu
        var biKhoa = await _context.TaiKhoans.AsNoTracking().Where(x => x.LoaiTaiKhoan == LoaiTaiKhoan.NHAN_VIEN && x.TrangThai == TrangThaiTaiKhoan.BI_KHOA)
            .Select(x => new { x.TenDN, HoTen = x.NhanVien!.HoTen, x.SoLanDangNhapSai }).ToListAsync();
        Them("TAI_KHOAN_NHAN_VIEN_BI_KHOA", "THONG_TIN", "Tài khoản nhân viên đang bị khóa",
            "Tài khoản bị khóa (tự động sau khi nhập sai mật khẩu nhiều lần, hoặc do Quản lý khóa).", "NHAN_VIEN",
            biKhoa.Select(x => $"{x.TenDN} ({x.HoTen})").ToList());

        var saoLuuGanNhat = await _context.LichSuSaoLuus.AsNoTracking()
            .Where(x => x.LoaiThaoTac == LoaiThaoTacSaoLuu.SAO_LUU && x.KetQua == KetQuaSaoLuu.THANH_CONG)
            .OrderByDescending(x => x.ThoiGianThucHien).Select(x => (DateTime?)x.ThoiGianThucHien).FirstOrDefaultAsync();
        if (saoLuuGanNhat is null)
            Them("SAO_LUU", "NGHIEM_TRONG", "Chưa từng sao lưu dữ liệu", "Hệ thống chưa có bản sao lưu thành công nào - hãy sao lưu ngay.", "SAO_LUU", new[] { "Chưa có bản sao lưu" });
        else if (bayGio - saoLuuGanNhat > TimeSpan.FromDays(_options.BackupStaleDays))
            Them("SAO_LUU", "CANH_BAO", "Đã lâu chưa sao lưu dữ liệu",
                $"Bản sao lưu gần nhất đã quá {_options.BackupStaleDays} ngày.", "SAO_LUU", new[] { $"Lần gần nhất: {saoLuuGanNhat:dd/MM/yyyy HH:mm}" });

        static int Thu(string m) => m switch { "NGHIEM_TRONG" => 0, "CANH_BAO" => 1, _ => 2 };
        canhBaos = canhBaos.OrderBy(x => Thu(x.MucDo)).ThenBy(x => x.Ma).ToList();
        var daXuLy = await GanTheoDoiAsync(canhBaos, bayGio);
        // Trong cùng mức độ, cảnh báo quá hạn lên trước.
        canhBaos = canhBaos.OrderBy(x => Thu(x.MucDo)).ThenByDescending(x => x.QuaHan).ThenBy(x => x.Ma).ToList();
        return new AlertReportDto
        {
            ThoiDiem = bayGio,
            NghiemTrong = canhBaos.Count(x => x.MucDo == "NGHIEM_TRONG"),
            CanhBao = canhBaos.Count(x => x.MucDo == "CANH_BAO"),
            ThongTin = canhBaos.Count(x => x.MucDo == "THONG_TIN"),
            CanhBaoDangCo = canhBaos,
            DaXuLyGanDay = daXuLy,
            QuaHan = canhBaos.Count(x => x.QuaHan),
            ChuaNhan = canhBaos.Count(x => x.TrangThaiXuLy == "CHUA_NHAN")
        };
    }

    /// <summary>
    /// Overbooking: (1) hai phiếu còn hiệu lực cùng giữ một phòng trong các ngày giao nhau - hệ thống có khóa nên không nên xảy ra, nếu thấy là dữ liệu lỗi;
    /// (2) phòng đã giữ cho khách đến hôm nay đang bảo trì (OOO) hoặc vẫn có khách cũ ở quá hạn - không thể giao phòng đúng hẹn.
    /// </summary>
    private async Task ThemCanhBaoTrungPhongAsync(List<AlertDto> canhBaos, DateOnly homNay)
    {
        var giu = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(x => TrangThaiGiuPhong.Contains(x.PhieuDatPhong.TrangThai) || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_NHAN_PHONG)
            .Select(x => new { x.MaPhong, x.MaPhieuDat, x.PhieuDatPhong.NgayDonDuKien, x.PhieuDatPhong.NgayTraDuKien, TraSom = x.PhieuDatPhong.PhieuNhanPhong != null
                && x.PhieuDatPhong.PhieuNhanPhong.ChiTietPhieuNhans.Any(n => n.MaPhong == x.MaPhong && n.TrangThai == TrangThaiChiTietPhieuNhan.DA_TRA_PHONG) })
            .ToListAsync();

        var trung = new List<string>();
        foreach (var nhom in giu.Where(x => !x.TraSom && x.NgayTraDuKien >= homNay).GroupBy(x => x.MaPhong))
        {
            var ds = nhom.OrderBy(x => x.NgayDonDuKien).ToList();
            for (var i = 0; i < ds.Count - 1; i++)
                for (var j = i + 1; j < ds.Count; j++)
                    if (ds[i].NgayDonDuKien < ds[j].NgayTraDuKien && ds[j].NgayDonDuKien < ds[i].NgayTraDuKien)
                        trung.Add($"{nhom.Key}: {ds[i].MaPhieuDat} và {ds[j].MaPhieuDat} trùng ngày");
        }
        if (trung.Count > 0)
            canhBaos.Add(new AlertDto
            {
                Ma = "OVERBOOKING", MucDo = "NGHIEM_TRONG", TieuDe = "Phòng bị giữ cho nhiều phiếu cùng lúc",
                MoTa = "Hai phiếu đặt còn hiệu lực cùng giữ một phòng trong các ngày giao nhau - cần đổi phòng cho một phiếu ngay.", ManHinh = "DAT_PHONG",
                SoLuong = trung.Count, ChiTiet = trung.Take(ToiDaChiTiet).ToList()
            });

        var denHomNay = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(x => x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN && x.PhieuDatPhong.NgayDonDuKien == homNay && x.PhieuDatPhong.PhieuNhanPhong == null)
            .Select(x => new { x.MaPhong, x.MaPhieuDat, x.Phong.TinhTrang }).ToListAsync();
        var khongSanSang = denHomNay.Where(x => x.TinhTrang is TinhTrangPhong.OOO or TinhTrangPhong.OC or TinhTrangPhong.OD)
            .Select(x => $"{x.MaPhieuDat}: phòng {x.MaPhong} đang {(x.TinhTrang == TinhTrangPhong.OOO ? "bảo trì" : "còn khách ở")}").ToList();
        if (khongSanSang.Count > 0)
            canhBaos.Add(new AlertDto
            {
                Ma = "PHONG_GIU_KHONG_SAN_SANG", MucDo = "NGHIEM_TRONG", TieuDe = "Phòng đã giữ cho khách đến hôm nay chưa sẵn sàng",
                MoTa = "Phòng giữ cho phiếu đến hôm nay đang bảo trì hoặc còn khách cũ - nguy cơ không có phòng giao đúng hẹn, cần đổi phòng.", ManHinh = "DAT_PHONG",
                SoLuong = khongSanSang.Count, ChiTiet = khongSanSang.Take(ToiDaChiTiet).ToList()
            });
    }
}
