using System.Globalization;
using System.Text;
using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Finance;
using KLCN060.Api.DTOs.Payments;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.Finance;

public interface ITransferReconciliationService
{
    Task<TransferReconOverviewDto> GetOverviewAsync(DateOnly? tuNgay, DateOnly? denNgay);
    Task<(List<TransferReconRowDto> Items, int Total)> ListAsync(TransferReconListQuery query);
    Task<TransferReconDetailDto> GetDetailAsync(int maThongBao);
    Task<BankNoticeDto> ResolveBankNoticeAsync(int maThongBao, ResolveBankNoticeRequest request, CurrentUser user);
    Task<(byte[] Content, string FileName)> ExportCsvAsync(TransferReconListQuery query, string? nguoiXuat = null);
    Task<(byte[] Content, string FileName)> ExportXlsxAsync(TransferReconListQuery query, string? nguoiXuat = null);
}

/// <summary>
/// Đối soát chuyển khoản từ ngân hàng đến booking / hóa đơn: mỗi thông báo "tiền về" được truy ngược ra mã giao dịch ngân hàng, số tiền thực nhận,
/// nội dung chuyển khoản, yêu cầu thanh toán, booking / hóa đơn, số phải thu, số đã ghi nhận, chênh lệch và lý do; mọi ngoại lệ được phân loại
/// kèm gợi ý xử lý. Xử lý ngoại lệ dùng <c>POST /reconciliation/bank-notifications/{id}/resolve</c> (có nhật ký, mỗi khoản chỉ xử lý một lần).
/// </summary>
public class TransferReconciliationService : ITransferReconciliationService
{
    private static readonly Dictionary<string, string> TenLoaiMap = new()
    {
        ["KHOP"] = "Đã khớp",
        ["THIEU_TIEN"] = "Thiếu tiền",
        ["THUA_TIEN"] = "Thừa tiền",
        ["SAI_NOI_DUNG"] = "Sai nội dung chuyển khoản",
        ["CHUA_XAC_DINH"] = "Giao dịch chưa xác định",
        ["HET_HAN"] = "Chuyển sau khi hết hạn",
        ["DA_HUY"] = "Chuyển cho yêu cầu đã hủy",
        ["THANH_TOAN_NHIEU_LAN"] = "Thanh toán nhiều lần",
        ["SAI_TAI_KHOAN"] = "Sai tài khoản nhận",
        ["KHONG_AP_DUNG"] = "Không áp dụng được"
    };

    private static readonly string[] ThuTuLoai = { "THIEU_TIEN", "THUA_TIEN", "SAI_NOI_DUNG", "CHUA_XAC_DINH", "HET_HAN", "DA_HUY", "THANH_TOAN_NHIEU_LAN", "SAI_TAI_KHOAN", "KHONG_AP_DUNG" };

    private readonly KLCN060DbContext _context;
    private readonly FinanceOptions _options;
    private readonly IAuditTrail _audit;

    public TransferReconciliationService(KLCN060DbContext context, IOptions<FinanceOptions> options, IAuditTrail audit)
    {
        _context = context;
        _options = options.Value;
        _audit = audit;
    }

    private int NgayQuaHan => Math.Max(_options.ThongBaoQuaHanNgay, 1);

    // =====================================================================================================
    // Nạp + phân loại
    // =====================================================================================================

    private sealed class Boi
    {
        public ThongBaoNganHang Tb { get; init; } = null!;
        public TransferReconRowDto Dong { get; init; } = null!;
        public GiaoDichChuyenKhoan? Gd { get; init; }
    }

    private async Task<List<Boi>> NapAsync(IQueryable<ThongBaoNganHang> nguon)
    {
        var tbs = await nguon.AsNoTracking().Include(x => x.GiaoDich)
            .OrderByDescending(x => x.ThoiGianNhan).ThenByDescending(x => x.MaThongBao).ToListAsync();
        if (tbs.Count == 0) return new();

        var gds = tbs.Select(x => x.GiaoDich).Where(x => x != null).Select(x => x!).DistinctBy(x => x.MaGiaoDich).ToList();
        var maHD = gds.Where(g => g.LoaiThamChieu == LoaiThamChieuThanhToan.HOA_DON).Select(g => g.MaThamChieu).ToList();
        var maCoc = gds.Where(g => g.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC).Select(g => g.MaThamChieu).ToList();

        var hoaDon = (await _context.HoaDons.AsNoTracking().Where(h => maHD.Contains(h.MaHD))
            .Select(h => new
            {
                h.MaHD,
                MaPhieuDat = h.PhieuNhanPhong.MaPhieuDat,
                Khach = h.PhieuNhanPhong.Khach != null ? h.PhieuNhanPhong.Khach.HoTen
                    : h.PhieuNhanPhong.PhieuDatPhong != null ? h.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : null
            }).ToListAsync()).ToDictionary(x => x.MaHD);
        var coc = (await _context.PhieuDatPhongs.AsNoTracking().Where(p => maCoc.Contains(p.MaPhieuDat))
            .Select(p => new { p.MaPhieuDat, Khach = p.Khach.HoTen }).ToListAsync()).ToDictionary(x => x.MaPhieuDat);

        var maTb = tbs.Select(x => x.MaThongBao).ToList();
        var tenNguoiXuLy = await TenNhieuAsync(tbs.Where(x => !string.IsNullOrEmpty(x.NguoiXuLy)).Select(x => x.NguoiXuLy!).Distinct().ToList());

        var bayGio = DateTime.Now;
        return tbs.Select(tb =>
        {
            var gd = tb.GiaoDich;
            var loai = PhanLoai(tb, gd);
            var dong = new TransferReconRowDto
            {
                MaThongBao = tb.MaThongBao, ThoiGianNganHang = tb.ThoiGianNganHang, ThoiGianNhan = tb.ThoiGianNhan,
                MaGiaoDichNganHang = tb.MaGiaoDichNganHang, SoTaiKhoanNhan = tb.SoTaiKhoanNhan, SoTienNganHang = tb.SoTien, NoiDung = tb.NoiDung,
                KetQua = tb.KetQua.ToString(), Loai = loai, TenLoai = TenLoaiMap[loai], LyDo = LyDo(tb, gd, loai), GoiYXuLy = GoiY(loai),
                TrangThaiXuLy = tb.TrangThaiXuLy.ToString(), NguoiXuLy = tb.NguoiXuLy is null ? null : tenNguoiXuLy.GetValueOrDefault(tb.NguoiXuLy, tb.NguoiXuLy), NgayXuLy = tb.NgayXuLy, HuongXuLy = tb.HuongXuLy, GhiChuXuLy = tb.GhiChuXuLy,
                SoTienDaGhiNhan = tb.KetQua == KetQuaDoiSoatNganHang.KHOP ? tb.SoTien : 0
            };
            if (gd is not null)
            {
                dong.MaGiaoDich = gd.MaGiaoDich; dong.TrangThaiGiaoDich = gd.TrangThai.ToString();
                dong.LoaiThamChieu = gd.LoaiThamChieu.ToString(); dong.MaThamChieu = gd.MaThamChieu; dong.SoTienPhaiThu = gd.SoTien;
                if (gd.LoaiThamChieu == LoaiThamChieuThanhToan.HOA_DON)
                {
                    dong.MaHoaDon = gd.MaThamChieu;
                    if (hoaDon.TryGetValue(gd.MaThamChieu, out var h)) { dong.MaPhieuDat = h.MaPhieuDat; dong.TenKhach = h.Khach; }
                }
                else
                {
                    dong.MaPhieuDat = gd.MaThamChieu;
                    if (coc.TryGetValue(gd.MaThamChieu, out var c)) dong.TenKhach = c.Khach;
                }
            }
            dong.ChenhLech = tb.KetQua switch
            {
                KetQuaDoiSoatNganHang.KHOP => 0,
                KetQuaDoiSoatNganHang.SAI_SO_TIEN when gd is not null => tb.SoTien - gd.SoTien,
                _ => tb.SoTien
            };
            if (tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
            {
                dong.SoNgayTonDong = Math.Max((int)(bayGio - tb.ThoiGianNhan).TotalDays, 0);
                dong.QuaHanXuLy = dong.SoNgayTonDong >= NgayQuaHan;
            }
            return new Boi { Tb = tb, Dong = dong, Gd = gd };
        }).ToList();
    }

    private static string PhanLoai(ThongBaoNganHang tb, GiaoDichChuyenKhoan? gd) => tb.KetQua switch
    {
        KetQuaDoiSoatNganHang.KHOP => "KHOP",
        KetQuaDoiSoatNganHang.SAI_SO_TIEN => gd is null ? "THIEU_TIEN" : tb.SoTien < gd.SoTien ? "THIEU_TIEN" : "THUA_TIEN",
        KetQuaDoiSoatNganHang.KHONG_TIM_THAY_GIAO_DICH => tb.NoiDung.Contains("KLCN060", StringComparison.OrdinalIgnoreCase) ? "SAI_NOI_DUNG" : "CHUA_XAC_DINH",
        KetQuaDoiSoatNganHang.GIAO_DICH_KHONG_CON_HIEU_LUC => gd?.TrangThai == TrangThaiGiaoDichChuyenKhoan.DA_HUY ? "DA_HUY" : "HET_HAN",
        KetQuaDoiSoatNganHang.GIAO_DICH_DA_THANH_TOAN => "THANH_TOAN_NHIEU_LAN",
        KetQuaDoiSoatNganHang.SAI_TAI_KHOAN_NHAN => "SAI_TAI_KHOAN",
        _ => "KHONG_AP_DUNG"
    };

    private static string Tien(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ";

    private static string LyDo(ThongBaoNganHang tb, GiaoDichChuyenKhoan? gd, string loai)
    {
        var s = loai switch
        {
            "KHOP" => $"Khớp yêu cầu {gd?.MaGiaoDich}: đúng nội dung, đúng số tiền {Tien(tb.SoTien)}, còn hạn - đã ghi nhận vào {(gd?.LoaiThamChieu == LoaiThamChieuThanhToan.HOA_DON ? "hóa đơn" : "tiền cọc")} {gd?.MaThamChieu}.",
            "THIEU_TIEN" => gd is null ? $"Số tiền {Tien(tb.SoTien)} không đúng số cần thanh toán." : $"Khách chuyển {Tien(tb.SoTien)} nhưng yêu cầu {gd.MaGiaoDich} cần {Tien(gd.SoTien)}: THIẾU {Tien(gd.SoTien - tb.SoTien)}. Hệ thống không tự ghi nhận.",
            "THUA_TIEN" => $"Khách chuyển {Tien(tb.SoTien)} nhưng yêu cầu {gd?.MaGiaoDich} chỉ cần {Tien(gd?.SoTien ?? 0)}: THỪA {Tien(tb.SoTien - (gd?.SoTien ?? 0))}. Hệ thống không tự ghi nhận.",
            "SAI_NOI_DUNG" => "Nội dung chuyển khoản có mã hệ thống nhưng không khớp yêu cầu thanh toán nào (sai/thiếu ký tự mã giao dịch).",
            "CHUA_XAC_DINH" => "Nội dung chuyển khoản không chứa mã giao dịch của hệ thống: chưa xác định được khoản này thuộc booking/hóa đơn nào.",
            "HET_HAN" => $"Tiền về sau khi yêu cầu {gd?.MaGiaoDich} đã hết hạn lúc {gd?.HanThanhToan:dd/MM/yyyy HH:mm}.",
            "DA_HUY" => $"Tiền về cho yêu cầu {gd?.MaGiaoDich} đã bị hủy.",
            "THANH_TOAN_NHIEU_LAN" => $"Yêu cầu {gd?.MaGiaoDich} đã được thanh toán trước đó (mã NH {gd?.MaGiaoDichNganHang}); đây là lần chuyển thêm.",
            "SAI_TAI_KHOAN" => $"Tiền chuyển vào tài khoản {tb.SoTaiKhoanNhan}, không phải tài khoản nhận của khách sạn.",
            _ => "Khớp yêu cầu nhưng nghiệp vụ không còn áp dụng được (VD hóa đơn/cọc đã thu đủ bằng cách khác)."
        };
        return !string.IsNullOrWhiteSpace(tb.GhiChu) && !s.Contains(tb.GhiChu!, StringComparison.OrdinalIgnoreCase) && loai != "KHOP" ? $"{s} Ghi chú hệ thống: {tb.GhiChu}" : s;
    }

    private static string GoiY(string loai) => loai switch
    {
        "THIEU_TIEN" => "Hệ thống chưa ghi nhận đồng nào của khoản này. Liên hệ khách chuyển bù phần thiếu rồi ghi nhận thủ công vào hóa đơn/cọc và chọn \"Đã xử lý ngoài hệ thống\".",
        "THUA_TIEN" => "Hệ thống chưa ghi nhận đồng nào của khoản này. Ghi nhận thủ công đúng số phải thu, rồi chọn \"Đã xử lý ngoài hệ thống\" và ghi rõ cách xử lý phần thừa (trả lại khách ngoài hệ thống).",
        "SAI_NOI_DUNG" or "CHUA_XAC_DINH" => "Hỏi khách theo tên người chuyển / số tiền / giờ chuyển. Xác định được: ghi nhận thủ công rồi chọn \"Đã xử lý ngoài hệ thống\". Không phải tiền của khách sạn: chọn \"Bỏ qua\".",
        "HET_HAN" or "DA_HUY" => "Kiểm tra booking/hóa đơn còn cần thu không. Còn cần: ghi nhận thủ công rồi chọn \"Đã xử lý ngoài hệ thống\". Không còn: trả lại khách ngoài hệ thống rồi chọn \"Đã xử lý ngoài hệ thống\" và ghi chú.",
        "THANH_TOAN_NHIEU_LAN" => "Khách đã chuyển trùng: trả lại khoản chuyển thừa ngoài hệ thống rồi chọn \"Đã xử lý ngoài hệ thống\" và ghi chú.",
        "SAI_TAI_KHOAN" => "Không phải tiền vào tài khoản khách sạn: chọn \"Bỏ qua\" và ghi chú (báo ngân hàng nếu cần).",
        "KHONG_AP_DUNG" => "Khoản đã được thu bằng cách khác: chọn \"Đã xử lý ngoài hệ thống\" và ghi rõ cách xử lý phần chuyển thừa.",
        _ => ""
    };

    private IQueryable<ThongBaoNganHang> LocThoiGian(IQueryable<ThongBaoNganHang> q, DateOnly? tu, DateOnly? den)
    {
        if (tu is { } a && den is { } b && a > b)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "tuNgay");
        if (tu is { } t0) { var d = t0.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGianNhan >= d); }
        if (den is { } t1) { var d = t1.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGianNhan < d); }
        return q;
    }

    // =====================================================================================================
    // Tổng quan
    // =====================================================================================================

    public async Task<TransferReconOverviewDto> GetOverviewAsync(DateOnly? tuNgay, DateOnly? denNgay)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var den = denNgay ?? homNay;
        var tu = tuNgay ?? den.AddDays(-29);

        var trongKy = await NapAsync(LocThoiGian(_context.ThongBaoNganHangs, tu, den));
        // Khoản chưa xử lý luôn được tính dù nằm ngoài kỳ: không để ngoại lệ "trôi mất" khi đổi khoảng ngày.
        var chuaXuLyNgoaiKy = await NapAsync(_context.ThongBaoNganHangs.Where(x => x.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
            .Where(x => !trongKy.Select(b => b.Tb.MaThongBao).Contains(x.MaThongBao)));
        var tatCa = trongKy.Concat(chuaXuLyNgoaiKy).ToList();

        var dto = new TransferReconOverviewDto { TuNgay = tu, DenNgay = den, CapNhatLuc = DateTime.Now, NgayQuaHan = NgayQuaHan };
        dto.ThongBaoMoiNhatLuc = await _context.ThongBaoNganHangs.MaxAsync(x => (DateTime?)x.ThoiGianNhan);

        foreach (var b in trongKy)
        {
            dto.TongThongBao++;
            dto.TongTienNganHangNhan += b.Tb.SoTien;
            if (b.Dong.Loai == "KHOP") { dto.SoKhop++; dto.TongTienDaKhop += b.Tb.SoTien; }
            else
            {
                dto.SoChuaKhop++;
                dto.TongChenhLech += Math.Abs(b.Dong.ChenhLech);
                if (b.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.DA_XU_LY) dto.SoDaXuLy++;
            }
        }
        foreach (var b in tatCa.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY))
        {
            dto.SoChuaXuLy++;
            dto.TongTienCanXuLy += b.Tb.SoTien;
            if (b.Dong.QuaHanXuLy) dto.SoChuaXuLyQuaHan++;
        }

        foreach (var loai in ThuTuLoai)
        {
            var nhom = tatCa.Where(x => x.Dong.Loai == loai).ToList();
            if (nhom.Count == 0) continue;
            dto.NhomNgoaiLe.Add(new TransferExceptionGroupDto
            {
                Loai = loai, Ten = TenLoaiMap[loai], SoLuong = nhom.Count, SoTien = nhom.Sum(x => x.Tb.SoTien),
                SoChuaXuLy = nhom.Count(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY),
                SoTienChuaXuLy = nhom.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY).Sum(x => x.Tb.SoTien)
            });
        }

        var bayGio = DateTime.Now;
        var cho = await _context.GiaoDichChuyenKhoans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.HanThanhToan > bayGio)
            .Select(x => x.SoTien).ToListAsync();
        dto.SoYeuCauChoThanhToan = cho.Count;
        dto.TienYeuCauChoThanhToan = cho.Sum();
        var d0 = tu.ToDateTime(TimeOnly.MinValue);
        var d1 = den.AddDays(1).ToDateTime(TimeOnly.MinValue);
        dto.SoYeuCauHetHan = await _context.GiaoDichChuyenKhoans.CountAsync(x =>
            (x.TrangThai == TrangThaiGiaoDichChuyenKhoan.HET_HAN || (x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN && x.HanThanhToan <= bayGio))
            && x.NgayTao >= d0 && x.NgayTao < d1);
        return dto;
    }

    // =====================================================================================================
    // Danh sách / chi tiết / xuất
    // =====================================================================================================

    private async Task<List<Boi>> LocAsync(TransferReconListQuery query)
    {
        var q = LocThoiGian(_context.ThongBaoNganHangs.AsQueryable(), query.TuNgay, query.DenNgay);
        if (!string.IsNullOrWhiteSpace(query.TrangThaiXuLy))
        {
            var tt = EnumParser.Parse<TrangThaiXuLyDoiSoat>(query.TrangThaiXuLy, "trangThaiXuLy", "trangThaiXuLy chỉ nhận: KHONG_CAN, CAN_XU_LY, DA_XU_LY.");
            q = q.Where(x => x.TrangThaiXuLy == tt);
        }
        var ds = await NapAsync(q);
        if (!string.IsNullOrWhiteSpace(query.Loai))
        {
            var loai = query.Loai.Trim().ToUpperInvariant();
            if (!TenLoaiMap.ContainsKey(loai))
                throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_KHONG_HOP_LE", "loai không hợp lệ.", "loai");
            ds = ds.Where(x => x.Dong.Loai == loai).ToList();
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            if (tuKhoa.Length > 100)
                throw new ApiException(StatusCodes.Status400BadRequest, "TU_KHOA_QUA_DAI", "Từ khóa tìm kiếm tối đa 100 ký tự.", "q");
            bool Co(string? s) => s != null && s.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase);
            ds = ds.Where(x => Co(x.Dong.MaGiaoDichNganHang) || Co(x.Dong.NoiDung) || Co(x.Dong.MaGiaoDich) || Co(x.Dong.MaThamChieu) || Co(x.Dong.MaPhieuDat)
                || Co(x.Dong.MaHoaDon) || Co(x.Dong.TenKhach) || Co(Math.Round(x.Dong.SoTienNganHang, 0).ToString(CultureInfo.InvariantCulture))).ToList();
        }
        return ds;
    }

    public async Task<(List<TransferReconRowDto> Items, int Total)> ListAsync(TransferReconListQuery query)
    {
        var ds = await LocAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        // Ngoại lệ chưa xử lý lên đầu (quá hạn trước), sau đó theo thời gian nhận mới nhất.
        var sapXep = ds.OrderByDescending(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
            .ThenByDescending(x => x.Dong.SoNgayTonDong).ThenByDescending(x => x.Tb.ThoiGianNhan).ThenByDescending(x => x.Tb.MaThongBao);
        return (sapXep.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).Select(x => x.Dong).ToList(), ds.Count);
    }

    public async Task<TransferReconDetailDto> GetDetailAsync(int maThongBao)
    {
        var b = (await NapAsync(_context.ThongBaoNganHangs.Where(x => x.MaThongBao == maThongBao))).FirstOrDefault()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_THONG_BAO", "Không tìm thấy thông báo tiền về.");
        var d = b.Dong;
        var chiTiet = new TransferReconDetailDto
        {
            MaThongBao = d.MaThongBao, ThoiGianNganHang = d.ThoiGianNganHang, ThoiGianNhan = d.ThoiGianNhan, MaGiaoDichNganHang = d.MaGiaoDichNganHang,
            SoTaiKhoanNhan = d.SoTaiKhoanNhan, SoTienNganHang = d.SoTienNganHang, NoiDung = d.NoiDung, KetQua = d.KetQua, Loai = d.Loai, TenLoai = d.TenLoai,
            LyDo = d.LyDo, GoiYXuLy = d.GoiYXuLy, MaGiaoDich = d.MaGiaoDich, TrangThaiGiaoDich = d.TrangThaiGiaoDich, LoaiThamChieu = d.LoaiThamChieu,
            MaThamChieu = d.MaThamChieu, MaPhieuDat = d.MaPhieuDat, MaHoaDon = d.MaHoaDon, TenKhach = d.TenKhach, SoTienPhaiThu = d.SoTienPhaiThu,
            SoTienDaGhiNhan = d.SoTienDaGhiNhan, ChenhLech = d.ChenhLech, TrangThaiXuLy = d.TrangThaiXuLy, SoNgayTonDong = d.SoNgayTonDong, QuaHanXuLy = d.QuaHanXuLy,
            NguoiXuLy = d.NguoiXuLy, NgayXuLy = d.NgayXuLy, HuongXuLy = d.HuongXuLy, GhiChuXuLy = d.GhiChuXuLy
        };

        if (b.Gd is { } gd)
        {
            chiTiet.NgayTaoYeuCau = gd.NgayTao; chiTiet.HanThanhToan = gd.HanThanhToan;
            chiTiet.NguoiTaoYeuCau = await TenAsync(gd.MaTaiKhoanTao);
            chiTiet.ThongBaoCungGiaoDich = (await NapAsync(_context.ThongBaoNganHangs.Where(x => x.MaGiaoDich == gd.MaGiaoDich && x.MaThongBao != maThongBao)))
                .Select(x => x.Dong).ToList();
        }

        var doiTuong = new List<string> { d.MaGiaoDichNganHang };
        if (d.MaGiaoDich is not null) doiTuong.Add(d.MaGiaoDich);
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking().Where(x => x.DoiTuongTacDong != null && doiTuong.Contains(x.DoiTuongTacDong))
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(30).ToListAsync();
        var ten = await TenNhieuAsync(nhatKy.Select(x => x.MaTaiKhoan).Distinct().ToList());
        chiTiet.LichSu = nhatKy.Select(x => new TransferReconHistoryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = ten.GetValueOrDefault(x.MaTaiKhoan, x.MaTaiKhoan), HanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();

        if (b.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
        {
            chiTiet.HuongXuLyChoPhep = new() { "DA_XU_LY_NGOAI", "BO_QUA" };
        }
        return chiTiet;
    }

    /// <summary>Xuất dữ liệu ngân hàng/khách ra tệp phải để lại dấu vết, như các báo cáo tài chính/quản trị khác.</summary>
    private async Task GhiNhatKyXuatAsync(string dinhDang, TransferReconListQuery query, int soDong)
    {
        _audit.Ghi("XUAT_BAO_CAO_TAI_CHINH", "DOI_SOAT_CHUYEN_KHOAN",
            $"baoCao=DOI_SOAT_CHUYEN_KHOAN; dinhDang={dinhDang}; tuNgay={query.TuNgay:yyyy-MM-dd}; denNgay={query.DenNgay:yyyy-MM-dd}; loai={query.Loai}; xuLy={query.TrangThaiXuLy}; tuKhoa={query.Q}; soDong={soDong}");
        await _context.SaveChangesAsync();
    }

    private async Task<string> TenAsync(string tenDN) => (await TenNhieuAsync(new List<string> { tenDN })).GetValueOrDefault(tenDN, tenDN);

    private async Task<Dictionary<string, string>> TenNhieuAsync(List<string> tenDNs)
    {
        if (tenDNs.Count == 0) return new();
        var ds = await _context.TaiKhoans.AsNoTracking().Where(x => tenDNs.Contains(x.TenDN))
            .Select(x => new
            {
                x.TenDN,
                HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null,
                Ma = x.NhanVien != null ? x.NhanVien.MaNV : x.Khach != null ? x.Khach.MaKhach : null
            }).ToListAsync();
        // Hiển thị "Họ tên (Mã NV)", ví dụ "Trần Thị Tân (NV02)" - không lộ tên đăng nhập.
        return ds.ToDictionary(x => x.TenDN, x => string.IsNullOrEmpty(x.HoTen) ? x.TenDN : string.IsNullOrEmpty(x.Ma) ? x.HoTen : $"{x.HoTen} ({x.Ma})");
    }

    // =====================================================================================================
    // Thông báo tiền về bất thường
    // =====================================================================================================

    public async Task<BankNoticeDto> ResolveBankNoticeAsync(int maThongBao, ResolveBankNoticeRequest request, CurrentUser user)
    {
        var huong = EnumParser.Parse<HuongXuLyDoiSoat>(request.Huong, "huong", "Huong chỉ nhận: DA_XU_LY_NGOAI, BO_QUA.");
        var ghiChu = request.GhiChu?.Trim();
        if (string.IsNullOrEmpty(ghiChu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_GHI_CHU", "Cần nhập ghi chú xử lý.", "ghiChu");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var tb = await _context.ThongBaoNganHangs.FirstOrDefaultAsync(x => x.MaThongBao == maThongBao)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_THONG_BAO", "Không tìm thấy thông báo tiền về.");
        if (tb.KetQua == KetQuaDoiSoatNganHang.KHOP)
            throw new ApiException(StatusCodes.Status409Conflict, "THONG_BAO_DA_KHOP", "Thông báo này đã khớp và được ghi nhận tự động, không cần xử lý.");

        // Chiếm thông báo bằng MỘT câu lệnh có điều kiện: hai kế toán cùng xử lý thì chỉ một người thắng.
        var bayGio = DateTime.Now;
        var soDong = await _context.ThongBaoNganHangs.Where(x => x.MaThongBao == maThongBao && x.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThaiXuLy, TrangThaiXuLyDoiSoat.DA_XU_LY).SetProperty(x => x.NguoiXuLy, user.TenDN)
                .SetProperty(x => x.NgayXuLy, bayGio).SetProperty(x => x.HuongXuLy, huong.ToString()).SetProperty(x => x.GhiChuXuLy, ghiChu));
        if (soDong == 0)
            throw new ApiException(StatusCodes.Status409Conflict, "THONG_BAO_DA_XU_LY", "Thông báo này đã được xử lý trước đó.");

        _audit.Ghi("XU_LY_THONG_BAO_NGAN_HANG", tb.MaGiaoDichNganHang,
            $"maThongBao={maThongBao}; ketQua={tb.KetQua}; soTien={Math.Round(tb.SoTien, 0)}; trangThaiXuLy={TrangThaiXuLyDoiSoat.CAN_XU_LY}->{TrangThaiXuLyDoiSoat.DA_XU_LY}; huong={huong}; ghiChu={ghiChu}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        _context.ChangeTracker.Clear();
        var capNhat = await _context.ThongBaoNganHangs.AsNoTracking().FirstAsync(x => x.MaThongBao == maThongBao);
        return ThongBaoMapper.ToDto(capNhat);
    }

    private static string TenTinhTrangXuLy(string? tt) => tt switch { "KHONG_CAN" => "Tự khớp (không cần xử lý)", "CAN_XU_LY" => "Chờ xử lý", "DA_XU_LY" => "Đã xử lý", _ => tt ?? "" };
    private static string TenHuongXuLy(string? h) => h switch { "DA_XU_LY_NGOAI" => "Đã xử lý ngoài hệ thống", "BO_QUA" => "Bỏ qua (không phải tiền khách sạn)", null or "" => "", _ => h };

    /// <summary>
    /// Xuất CSV (UTF-8 có BOM, mở thẳng bằng Excel): khối tiêu đề (kỳ, bộ lọc, người/giờ xuất), khối tổng hợp, bảng phân loại ngoại lệ, rồi bảng chi tiết có STT,
    /// giá trị đã Việt hóa (không còn mã nội bộ), người xử lý dạng "Họ tên (Mã NV)", cột xử lý ghi rõ "—" khi chưa/không cần xử lý, và dòng tổng cuối bảng.
    /// </summary>
    public async Task<(byte[] Content, string FileName)> ExportCsvAsync(TransferReconListQuery query, string? nguoiXuat = null)
    {
        var ds = (await LocAsync(query)).OrderBy(x => x.Tb.ThoiGianNhan).ThenBy(x => x.Tb.MaThongBao).ToList();
        var vn = CultureInfo.GetCultureInfo("vi-VN");
        static string C(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s[0] is '=' or '+' or '@' or '-' or '\t' or '\r') s = "'" + s;   // chống chèn công thức khi mở bằng Excel
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        static string N(decimal? v) => v is null ? "" : Math.Round(v.Value, 0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        string D(DateTime? d, string f = "dd/MM/yyyy HH:mm") => d is null ? "" : d.Value.ToString(f, vn);
        string Dong(params string[] o) => string.Join(",", o);

        var tu = query.TuNgay; var den = query.DenNgay;
        var loc = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Loai)) loc.Add("Loại khoản: " + (TenLoaiMap.GetValueOrDefault(query.Loai.Trim().ToUpperInvariant(), query.Loai)));
        if (!string.IsNullOrWhiteSpace(query.TrangThaiXuLy)) loc.Add("Xử lý: " + TenTinhTrangXuLy(query.TrangThaiXuLy.Trim().ToUpperInvariant()));
        if (!string.IsNullOrWhiteSpace(query.Q)) loc.Add("Từ khóa: " + query.Q.Trim());
        var tenNguoiXuat = string.IsNullOrWhiteSpace(nguoiXuat) ? "" : await TenAsync(nguoiXuat);

        var sb = new StringBuilder();
        sb.AppendLine(Dong(C("BÁO CÁO ĐỐI SOÁT CHUYỂN KHOẢN")));
        sb.AppendLine(Dong(C("Kỳ báo cáo"), C($"{(tu is null ? "từ đầu" : tu.Value.ToString("dd/MM/yyyy"))} - {(den is null ? "nay" : den.Value.ToString("dd/MM/yyyy"))} (theo ngày hệ thống nhận thông báo)")));
        sb.AppendLine(Dong(C("Bộ lọc"), C(loc.Count == 0 ? "Không lọc (tất cả)" : string.Join("; ", loc))));
        sb.AppendLine(Dong(C("Xuất lúc"), C(DateTime.Now.ToString("dd/MM/yyyy HH:mm", vn)), C("Người xuất"), C(tenNguoiXuat)));
        sb.AppendLine();

        var khop = ds.Where(x => x.Dong.Loai == "KHOP").ToList();
        var chuaKhop = ds.Where(x => x.Dong.Loai != "KHOP").ToList();
        var canXuLy = ds.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY).ToList();
        sb.AppendLine(Dong(C("TỔNG HỢP"), C("Số khoản"), C("Số tiền (đ)")));
        sb.AppendLine(Dong(C("Ngân hàng báo đã nhận"), ds.Count.ToString(), N(ds.Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("Đã khớp, tự ghi nhận"), khop.Count.ToString(), N(khop.Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("Chưa khớp (ngoại lệ)"), chuaKhop.Count.ToString(), N(chuaKhop.Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("   - trong đó đã xử lý"), chuaKhop.Count(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.DA_XU_LY).ToString(), N(chuaKhop.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.DA_XU_LY).Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("   - trong đó chờ xử lý"), canXuLy.Count.ToString(), N(canXuLy.Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("   - chờ xử lý quá hạn (" + NgayQuaHan + " ngày)"), canXuLy.Count(x => x.Dong.QuaHanXuLy).ToString(), N(canXuLy.Where(x => x.Dong.QuaHanXuLy).Sum(x => x.Tb.SoTien))));
        sb.AppendLine(Dong(C("Tổng |chênh lệch| các khoản lệch"), "", N(chuaKhop.Sum(x => Math.Abs(x.Dong.ChenhLech)))));
        sb.AppendLine();

        sb.AppendLine(Dong(C("PHÂN LOẠI NGOẠI LỆ"), C("Số khoản"), C("Số tiền (đ)"), C("Chờ xử lý")));
        foreach (var loai in ThuTuLoai)
        {
            var nhom = ds.Where(x => x.Dong.Loai == loai).ToList();
            if (nhom.Count == 0) continue;
            sb.AppendLine(Dong(C(TenLoaiMap[loai]), nhom.Count.ToString(), N(nhom.Sum(x => x.Tb.SoTien)), nhom.Count(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY).ToString()));
        }
        sb.AppendLine();

        sb.AppendLine(Dong(C("CHI TIẾT")));
        sb.AppendLine(string.Join(",", new[]
        {
            "STT", "Nhận lúc", "Giờ ngân hàng", "Mã GD ngân hàng", "Số tiền ngân hàng (đ)", "Nội dung chuyển khoản", "Kết quả đối soát", "Lý do",
            "Mã yêu cầu", "Booking", "Hóa đơn", "Khách", "Phải thu (đ)", "Đã ghi nhận (đ)", "Chênh lệch (đ)",
            "Tình trạng xử lý", "Số ngày tồn đọng", "Người xử lý", "Ngày xử lý", "Hướng xử lý", "Ghi chú xử lý"
        }.Select(C)));
        const string KhongCo = "—";
        var stt = 0;
        foreach (var x in ds.Select(x => x.Dong))
        {
            var chuaXuLy = x.TrangThaiXuLy != "DA_XU_LY";
            sb.AppendLine(Dong(
                (++stt).ToString(), C(D(x.ThoiGianNhan)), C(D(x.ThoiGianNganHang)), C(x.MaGiaoDichNganHang), N(x.SoTienNganHang), C(x.NoiDung), C(x.TenLoai), C(x.LyDo),
                C(x.MaGiaoDich), C(x.MaPhieuDat), C(x.MaHoaDon), C(x.TenKhach), N(x.SoTienPhaiThu), N(x.SoTienDaGhiNhan), N(x.ChenhLech),
                C(TenTinhTrangXuLy(x.TrangThaiXuLy)), x.TrangThaiXuLy == "CAN_XU_LY" ? x.SoNgayTonDong.ToString() : "",
                C(chuaXuLy ? KhongCo : x.NguoiXuLy), C(chuaXuLy ? KhongCo : D(x.NgayXuLy)), C(chuaXuLy ? KhongCo : TenHuongXuLy(x.HuongXuLy)), C(chuaXuLy ? KhongCo : x.GhiChuXuLy)));
        }
        var dongs = ds.Select(x => x.Dong).ToList();
        sb.AppendLine(Dong(C("TỔNG (chênh lệch cộng theo giá trị tuyệt đối)"), "", "", "", N(dongs.Sum(x => x.SoTienNganHang)), "", "", "", "", "", "", "", N(dongs.Sum(x => x.SoTienPhaiThu ?? 0)), N(dongs.Sum(x => x.SoTienDaGhiNhan)), N(dongs.Sum(x => Math.Abs(x.ChenhLech))), "", "", "", "", "", ""));   // đệm đủ 21 cột như bảng chi tiết
        await GhiNhatKyXuatAsync("csv", query, ds.Count);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, TenTepXuat.Tao("DoiSoatChuyenKhoan", "csv"));
    }

    /// <summary>
    /// Xuất Excel (.xlsx) 2 trang tính: "Tổng hợp" (thẻ chỉ số, phân loại ngoại lệ, cách đọc) và "Chi tiết" (từng khoản, có nút lọc, cố định dòng đầu cột,
    /// số tiền / ngày giờ là số / ngày thật, dòng tổng). Cùng dữ liệu và cách Việt hóa với bản CSV.
    /// </summary>
    public async Task<(byte[] Content, string FileName)> ExportXlsxAsync(TransferReconListQuery query, string? nguoiXuat = null)
    {
        var ds = (await LocAsync(query)).OrderBy(x => x.Tb.ThoiGianNhan).ThenBy(x => x.Tb.MaThongBao).ToList();
        var tu = query.TuNgay; var den = query.DenNgay;
        var loc = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Loai)) loc.Add("Loại khoản: " + TenLoaiMap.GetValueOrDefault(query.Loai.Trim().ToUpperInvariant(), query.Loai));
        if (!string.IsNullOrWhiteSpace(query.TrangThaiXuLy)) loc.Add("Xử lý: " + TenTinhTrangXuLy(query.TrangThaiXuLy.Trim().ToUpperInvariant()));
        if (!string.IsNullOrWhiteSpace(query.Q)) loc.Add("Từ khóa: " + query.Q.Trim());
        var tenNguoiXuat = string.IsNullOrWhiteSpace(nguoiXuat) ? "" : await TenAsync(nguoiXuat);

        var khop = ds.Where(x => x.Dong.Loai == "KHOP").ToList();
        var chuaKhop = ds.Where(x => x.Dong.Loai != "KHOP").ToList();
        var daXuLy = chuaKhop.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.DA_XU_LY).ToList();
        var canXuLy = ds.Where(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY).ToList();
        var quaHan = canXuLy.Where(x => x.Dong.QuaHanXuLy).ToList();

        var tl = new TaiLieuBaoCao
        {
            TieuDe = "Báo cáo đối soát chuyển khoản",
            KyBaoCao = $"Kỳ: {(tu is null ? "từ đầu" : tu.Value.ToString("dd/MM/yyyy"))} - {(den is null ? "nay" : den.Value.ToString("dd/MM/yyyy"))} (theo ngày hệ thống nhận thông báo) · Bộ lọc: {(loc.Count == 0 ? "không lọc (tất cả)" : string.Join("; ", loc))}",
            NguoiXuat = tenNguoiXuat,
            XuatLuc = DateTime.Now
        };

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string Dong(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("N0", vi) + " đ";
        var tongHop = new PhanBaoCao("Tổng hợp", "Tổng hợp", "Bức tranh chung của các khoản tiền ngân hàng báo đã nhận trong kỳ và tình trạng xử lý các khoản lệch.", new List<KhoiBaoCao>());
        tongHop.Khois.Add(new KhoiThe(new List<TheSo>
        {
            new("Ngân hàng báo đã nhận", $"{ds.Count:N0} khoản", Dong(ds.Sum(x => x.Tb.SoTien))),
            new("Đã khớp, tự ghi nhận", $"{khop.Count:N0} khoản", Dong(khop.Sum(x => x.Tb.SoTien)), "tot"),
            new("Chưa khớp (ngoại lệ)", $"{chuaKhop.Count:N0} khoản", Dong(chuaKhop.Sum(x => x.Tb.SoTien)), chuaKhop.Count > 0 ? "xau" : "tot"),
            new("Trong đó đã xử lý", $"{daXuLy.Count:N0} khoản", Dong(daXuLy.Sum(x => x.Tb.SoTien))),
            new("Trong đó chờ xử lý", $"{canXuLy.Count:N0} khoản", Dong(canXuLy.Sum(x => x.Tb.SoTien)), canXuLy.Count > 0 ? "xau" : "tot"),
            new($"Chờ xử lý quá hạn ({NgayQuaHan} ngày)", $"{quaHan.Count:N0} khoản", Dong(quaHan.Sum(x => x.Tb.SoTien)), quaHan.Count > 0 ? "xau" : "tot"),
            new("Tổng |chênh lệch| các khoản lệch", Dong(chuaKhop.Sum(x => Math.Abs(x.Dong.ChenhLech))))
        }));
        var phanLoai = new BangBaoCao
        {
            TieuDe = "Phân loại ngoại lệ",
            PhuDe = "Mỗi khoản chuyển khoản thuộc đúng một loại.",
            Cot = { new("Loại", KieuCot.Chu), new("Số khoản", KieuCot.So), new("Số tiền (đ)", KieuCot.Tien), new("Chờ xử lý", KieuCot.So) }
        };
        foreach (var loai in ThuTuLoai)
        {
            var nhom = ds.Where(x => x.Dong.Loai == loai).ToList();
            if (nhom.Count == 0) continue;
            phanLoai.ThemDong(TenLoaiMap[loai], nhom.Count, Math.Round(nhom.Sum(x => x.Tb.SoTien), 0), nhom.Count(x => x.Tb.TrangThaiXuLy == TrangThaiXuLyDoiSoat.CAN_XU_LY));
        }
        phanLoai.Tong = new object?[] { "Tổng", ds.Count, Math.Round(ds.Sum(x => x.Tb.SoTien), 0), canXuLy.Count };
        tongHop.Khois.Add(new KhoiBang(phanLoai));
        tongHop.Khois.Add(new KhoiGhiChu("Cách đọc", new List<string>
        {
            "Đã khớp: thông báo ngân hàng khớp đúng yêu cầu thanh toán, hệ thống đã tự ghi nhận - không cần làm gì thêm.",
            "Chưa khớp (ngoại lệ): sai số tiền, sai nội dung, trùng, không tìm thấy yêu cầu... cần có người xử lý và ghi rõ hướng xử lý.",
            "Chênh lệch = Số tiền ngân hàng nhận - Số tiền phải thu; ở dòng tổng được cộng theo giá trị tuyệt đối.",
            "Trang \"Chi tiết\" có nút lọc ở dòng đầu cột để lọc nhanh theo loại, tình trạng, người xử lý..."
        }));
        tl.Phans.Add(tongHop);

        var ct = new BangBaoCao
        {
            TieuDe = "Chi tiết từng khoản",
            PhuDe = "Sắp theo thời gian hệ thống nhận. Cột xử lý ghi \"—\" khi khoản chưa / không cần xử lý.",
            Cot =
            {
                new("STT", KieuCot.So), new("Nhận lúc", KieuCot.NgayGio), new("Giờ ngân hàng", KieuCot.NgayGio), new("Mã GD ngân hàng"), new("Số tiền ngân hàng (đ)", KieuCot.Tien),
                new("Nội dung chuyển khoản"), new("Kết quả đối soát"), new("Lý do"), new("Mã yêu cầu"), new("Booking"), new("Hóa đơn"), new("Khách"),
                new("Phải thu (đ)", KieuCot.Tien), new("Đã ghi nhận (đ)", KieuCot.Tien), new("Chênh lệch (đ)", KieuCot.Tien),
                new("Tình trạng xử lý"), new("Số ngày tồn đọng", KieuCot.So), new("Người xử lý"), new("Ngày xử lý", KieuCot.NgayGio), new("Hướng xử lý"), new("Ghi chú xử lý")
            }
        };
        const string KhongCo = "—";
        var stt = 0;
        foreach (var x in ds.Select(x => x.Dong))
        {
            var chuaXuLy = x.TrangThaiXuLy != "DA_XU_LY";
            ct.ThemDong(++stt, x.ThoiGianNhan, x.ThoiGianNganHang, x.MaGiaoDichNganHang ?? "", Math.Round(x.SoTienNganHang, 0), x.NoiDung ?? "", x.TenLoai ?? "", x.LyDo ?? "",
                x.MaGiaoDich ?? "", x.MaPhieuDat ?? "", x.MaHoaDon ?? "", x.TenKhach ?? "",
                x.SoTienPhaiThu is null ? null : Math.Round(x.SoTienPhaiThu.Value, 0), Math.Round(x.SoTienDaGhiNhan, 0), Math.Round(x.ChenhLech, 0),
                TenTinhTrangXuLy(x.TrangThaiXuLy), x.TrangThaiXuLy == "CAN_XU_LY" ? x.SoNgayTonDong : null,
                chuaXuLy ? KhongCo : x.NguoiXuLy ?? KhongCo, chuaXuLy ? null : x.NgayXuLy, chuaXuLy ? KhongCo : TenHuongXuLy(x.HuongXuLy), chuaXuLy ? KhongCo : x.GhiChuXuLy ?? KhongCo);
        }
        var dongs = ds.Select(x => x.Dong).ToList();
        var tong = new object?[21];
        tong[0] = "TỔNG"; tong[4] = Math.Round(dongs.Sum(x => x.SoTienNganHang), 0); tong[12] = Math.Round(dongs.Sum(x => x.SoTienPhaiThu ?? 0), 0);
        tong[13] = Math.Round(dongs.Sum(x => x.SoTienDaGhiNhan), 0); tong[14] = Math.Round(dongs.Sum(x => Math.Abs(x.ChenhLech)), 0);
        ct.Tong = tong;
        tl.Phans.Add(new PhanBaoCao("Chi tiết", "Chi tiết", "Chênh lệch ở dòng tổng cộng theo giá trị tuyệt đối.", new List<KhoiBaoCao> { new KhoiBang(ct) }, true));

        await GhiNhatKyXuatAsync("xlsx", query, ds.Count);
        return (ManagementReportWriter.ToXlsx(tl), TenTepXuat.Tao("DoiSoatChuyenKhoan", "xlsx"));
    }
}

internal static class ThongBaoMapper
{
    public static BankNoticeDto ToDto(ThongBaoNganHang x) => new()
    {
        MaThongBao = x.MaThongBao, MaGiaoDichNganHang = x.MaGiaoDichNganHang, SoTaiKhoanNhan = x.SoTaiKhoanNhan, SoTien = x.SoTien, NoiDung = x.NoiDung,
        ThoiGianNganHang = x.ThoiGianNganHang, ThoiGianNhan = x.ThoiGianNhan, KetQua = x.KetQua.ToString(), MaGiaoDich = x.MaGiaoDich, GhiChu = x.GhiChu,
        TrangThaiXuLy = x.TrangThaiXuLy.ToString(), NguoiXuLy = x.NguoiXuLy, NgayXuLy = x.NgayXuLy, HuongXuLy = x.HuongXuLy, GhiChuXuLy = x.GhiChuXuLy
    };
}
