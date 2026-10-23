using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.DTOs.FrontDesk;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IFrontDeskService
{
    Task<FrontDeskOverviewDto> GetOverviewAsync();
    Task<List<ArrivalDto>> GetArrivalsAsync(DateOnly? ngay);
    Task<List<DepartureDto>> GetDeparturesAsync(DateOnly? ngay);
    Task<List<DepartureDto>> GetInHouseAsync();
    Task<List<VacantRoomDto>> GetVacantRoomsAsync(DateOnly? nhan, DateOnly? tra, string? maLoai);
    Task<NoShowReportDto> GetNoShowsAsync(DateOnly? tuNgay, DateOnly? denNgay);
}

/// <summary>
/// Số liệu vận hành cho lễ tân trong ca (chỉ đọc): tình hình phòng, khách đến/đi hôm nay, khách đang lưu trú, phòng trống theo khoảng ngày, khách không đến.
/// Không cần quyền báo cáo của Quản lý: mỗi endpoint chỉ đòi quyền xem đúng loại dữ liệu nó trả về (phòng / đặt phòng / lưu trú).
/// </summary>
public class FrontDeskService : IFrontDeskService
{
    private const int SoViecToiDa = 200;
    private static readonly string DangO = nameof(TrangThaiChiTietPhieuNhan.DANG_O);
    private static readonly string DaTra = nameof(TrangThaiChiTietPhieuNhan.DA_TRA_PHONG);
    private const int SoNgayKhongDenMacDinh = 7;
    private const int SoNgayToiDa = 92;

    private readonly KLCN060DbContext _context;
    private readonly IBookingService _bookings;

    public FrontDeskService(KLCN060DbContext context, IBookingService bookings)
    {
        _context = context;
        _bookings = bookings;
    }

    private static DateOnly HomNay => DateOnly.FromDateTime(DateTime.Now);

    // Phiếu còn giữ phòng nhưng khách chưa nhận (kể cả nhận một phần thì trạng thái vẫn DA_XAC_NHAN).
    private static bool ConGiuPhong(TrangThaiPhieuDat t) => t is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN;

    public async Task<FrontDeskOverviewDto> GetOverviewAsync()
    {
        var homNay = HomNay;
        var dto = new FrontDeskOverviewDto { Ngay = homNay, CapNhatLuc = DateTime.Now };

        // ----- Phòng -----
        var phongs = await _context.Phongs.AsNoTracking().Select(x => new { x.MaPhong, x.TinhTrang }).ToListAsync();
        dto.TongSoPhong = phongs.Count;
        dto.PhongTrong = phongs.Count(x => x.TinhTrang is TinhTrangPhong.VC or TinhTrangPhong.VI);
        dto.PhongDangCoKhach = phongs.Count(x => x.TinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD);
        dto.PhongChoDon = phongs.Count(x => x.TinhTrang == TinhTrangPhong.VD);
        dto.PhongCoKhachCanDon = phongs.Count(x => x.TinhTrang == TinhTrangPhong.OD);
        dto.PhongDangDon = await _context.PhieuDonPhongs.AsNoTracking().CountAsync(x => x.TrangThai == TrangThaiPhieuDon.DANG_DON);
        dto.PhongBaoTri = phongs.Count(x => x.TinhTrang == TinhTrangPhong.OOO);

        // Phòng được giữ cho khách đến hôm nay (chưa nhận) => không bán được cho khách vãng lai.
        var giuHomNay = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => (ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                         && ct.PhieuDatPhong.NgayDonDuKien <= homNay && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .Select(ct => ct.MaPhong).Distinct().ToListAsync();
        var giuSet = giuHomNay.ToHashSet();
        dto.PhongTrongBanDuoc = phongs.Count(x => x.TinhTrang is TinhTrangPhong.VC or TinhTrangPhong.VI && !giuSet.Contains(x.MaPhong));

        // ----- Khách đến hôm nay -----
        var denHomNay = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.NgayDonDuKien == homNay && x.TrangThai != TrangThaiPhieuDat.DA_HUY && x.TrangThai != TrangThaiPhieuDat.KHONG_DEN)
            .Select(x => new { x.TrangThai, SoPhong = x.ChiTietPhieuDats.Count, DaNhan = x.PhieuNhanPhong == null ? 0 : x.PhieuNhanPhong.ChiTietPhieuNhans.Count })
            .ToListAsync();
        dto.BookingDenHomNay = denHomNay.Count;
        var choNhan = denHomNay.Where(x => ConGiuPhong(x.TrangThai)).ToList();
        dto.BookingChoNhanPhong = choNhan.Count;
        dto.PhongSapCheckIn = choNhan.Sum(x => x.SoPhong - x.DaNhan);
        dto.BookingDaNhanHomNay = denHomNay.Count - choNhan.Count;

        // ----- Khách đi / đang ở -----
        var dangO = await LayLuotOAsync(x => x.TrangThai == DangO);
        dto.KhachDangLuuTru = dangO.Count;
        dto.PhongSapCheckOut = dangO.Count(x => x.NgayTraDuKien == homNay);
        dto.PhongQuaHanTra = dangO.Count(x => x.QuaHan);
        dto.PhongDaTraHomNay = await _context.ChiTietPhieuNhans.AsNoTracking().CountAsync(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DA_TRA_PHONG && x.NgayTra == homNay);

        dto.PhongGiuChoKhachDen = dto.PhongTrong - dto.PhongTrongBanDuoc;

        // ----- Việc cần xử lý -----
        var viec = new List<FrontDeskTaskDto>();
        var bookingCanXuLy = new HashSet<string>();

        var choCoc = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN && x.NgayDonDuKien >= homNay && x.NgayDonDuKien <= homNay.AddDays(1))
            .OrderBy(x => x.NgayDonDuKien).Select(x => new { x.MaPhieuDat, x.NgayDonDuKien, x.Khach.HoTen, x.TienCoc }).ToListAsync();
        dto.DatCocChoThu = choCoc.Count;
        dto.TongTienCocChoThu = choCoc.Sum(x => x.TienCoc);
        foreach (var x in choCoc)
        {
            bookingCanXuLy.Add(x.MaPhieuDat);
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "CHO_COC", MucDo = x.NgayDonDuKien == homNay ? "CANH_BAO" : "THONG_TIN", MaThamChieu = x.MaPhieuDat,
                MoTa = $"{x.HoTen} đến {(x.NgayDonDuKien == homNay ? "hôm nay" : "ngày mai")} nhưng chưa xác nhận đặt cọc ({x.TienCoc:N0}đ)",
                ManHinhDich = "DAT_PHONG", HanhDong = "Mở booking · thu cọc"
            });
        }

        var quaHan = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => (x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN) && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null)
            .OrderBy(x => x.NgayDonDuKien).Select(x => new { x.MaPhieuDat, x.NgayDonDuKien, x.Khach.HoTen }).ToListAsync();
        dto.KhongDenChoXuLy = quaHan.Count;
        foreach (var x in quaHan)
        {
            bookingCanXuLy.Add(x.MaPhieuDat);
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "QUA_HAN_KHONG_DEN", MucDo = "CANH_BAO", MaThamChieu = x.MaPhieuDat,
                MoTa = $"{x.HoTen} quá ngày đón {x.NgayDonDuKien:dd/MM/yyyy} mà chưa nhận phòng: liên hệ khách hoặc đánh dấu không đến",
                ManHinhDich = "DAT_PHONG", HanhDong = "Mở booking · xử lý không đến"
            });
        }

        viec.AddRange(dangO.Where(x => x.QuaHan).OrderBy(x => x.NgayTraDuKien).Select(x => new FrontDeskTaskDto
        {
            Loai = "TRA_PHONG_QUA_HAN", MucDo = "CANH_BAO", MaThamChieu = $"{x.MaPhieuNhan}/{x.MaPhong}", MaPhong = x.MaPhong, MaPhieuNhan = x.MaPhieuNhan,
            MoTa = $"Phòng {x.MaPhong} ({x.HoTenKhach}) quá ngày trả dự kiến {x.NgayTraDuKien:dd/MM/yyyy}: làm thủ tục trả phòng hoặc gia hạn",
            ManHinhDich = "TRA_PHONG", HanhDong = "Xem folio · trả phòng"
        }));

        // Mỗi booking một việc (phiếu nhiều phòng chưa sẵn sàng không bị đếm nhiều lần).
        var chuaSanSang = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => ct.PhieuDatPhong.NgayDonDuKien == homNay && ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN
                         && (ct.Phong.TinhTrang == TinhTrangPhong.VD || ct.Phong.TinhTrang == TinhTrangPhong.OOO || ct.Phong.TinhTrang == TinhTrangPhong.OC || ct.Phong.TinhTrang == TinhTrangPhong.OD)
                         && (ct.PhieuDatPhong.PhieuNhanPhong == null || !ct.PhieuDatPhong.PhieuNhanPhong.ChiTietPhieuNhans.Any(n => n.MaPhong == ct.MaPhong)))
            .Select(ct => new { ct.MaPhieuDat, ct.MaPhong, ct.Phong.TinhTrang, ct.PhieuDatPhong.Khach.HoTen }).ToListAsync();
        foreach (var g in chuaSanSang.GroupBy(x => x.MaPhieuDat).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            bookingCanXuLy.Add(g.Key);
            var phongDs = string.Join(", ", g.OrderBy(x => x.MaPhong, StringComparer.Ordinal).Select(x => $"{x.MaPhong} ({TenTinhTrang(x.TinhTrang)})"));
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "PHONG_CHUA_SAN_SANG", MucDo = g.All(x => x.TinhTrang == TinhTrangPhong.VD) ? "CANH_BAO" : "NGHIEM_TRONG", MaThamChieu = g.Key,
                MaPhong = g.Min(x => x.MaPhong),
                MoTa = $"Phòng giữ cho {g.First().HoTen} (đến hôm nay) chưa sẵn sàng: {phongDs}",
                ManHinhDich = "DAT_PHONG", HanhDong = "Mở booking · đổi phòng"
            });
        }

        // Hóa đơn còn nợ: Tổng tiền - tổng các lần đã thu (cùng cách tính với màn Hóa đơn), không dựa vào cờ trạng thái.
        var hoaDons = await _context.HoaDons.AsNoTracking()
            .Select(x => new { x.MaHD, x.NgayLap, x.TongTien, DaThu = x.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0m, KhachTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen : null })
            .ToListAsync();
        var conNo = hoaDons.Where(x => x.TongTien - x.DaThu > 0).OrderBy(x => x.NgayLap).ToList();
        dto.HoaDonConNo = conNo.Count;
        dto.TongConPhaiThu = conNo.Sum(x => x.TongTien - x.DaThu);
        viec.AddRange(conNo.Take(50).Select(x => new FrontDeskTaskDto
        {
            Loai = "HOA_DON_CHUA_THU", MucDo = "THONG_TIN", MaThamChieu = x.MaHD,
            MoTa = $"Hóa đơn {x.MaHD}{(x.KhachTen is null ? "" : $" ({x.KhachTen})")} lập {x.NgayLap:dd/MM/yyyy}: còn phải thu {(x.TongTien - x.DaThu):N0}đ",
            ManHinhDich = "THANH_TOAN", HanhDong = "Mở hóa đơn · thu tiền"
        }));
        // Danh sách việc chỉ liệt kê 50 hóa đơn cũ nhất: có thêm thì báo rõ để lễ tân biết còn hóa đơn nợ chưa hiện (ô công nợ vẫn đếm đủ).
        if (conNo.Count > 50)
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "HOA_DON_CON_NUA", MucDo = "THONG_TIN", MaThamChieu = $"+{conNo.Count - 50} hóa đơn",
                MoTa = $"Còn {conNo.Count - 50} hóa đơn khác còn nợ chưa hiện ở danh sách này (chỉ hiện 50 hóa đơn cũ nhất). Mở màn Hóa đơn để xem đủ.",
                ManHinhDich = "THANH_TOAN", HanhDong = "Mở màn Hóa đơn · lọc còn nợ"
            });

        // Phòng bảo trì: lý do + ai khóa + từ khi nào lấy từ lịch sử tình trạng phòng.
        var phongBaoTri = phongs.Where(x => x.TinhTrang == TinhTrangPhong.OOO).Select(x => x.MaPhong).ToList();
        if (phongBaoTri.Count > 0)
        {
            var khoa = (await _context.LichSuTinhTrangPhongs.AsNoTracking()
                    .Where(x => phongBaoTri.Contains(x.MaPhong) && x.DenTinhTrang == TinhTrangPhong.OOO)
                    .Select(x => new { x.MaPhong, x.ThoiGian, x.LyDo, x.MaTaiKhoan }).ToListAsync())
                .GroupBy(x => x.MaPhong).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ThoiGian).First());
            foreach (var ma in phongBaoTri.OrderBy(x => x, StringComparer.Ordinal))
            {
                var k = khoa.GetValueOrDefault(ma);
                viec.Add(new FrontDeskTaskDto
                {
                    Loai = "PHONG_BAO_TRI", MucDo = "THONG_TIN", MaThamChieu = ma, MaPhong = ma,
                    MoTa = k is null ? $"Phòng {ma} đang bảo trì (chưa có lịch sử khóa)"
                        : $"Phòng {ma} bảo trì từ {k.ThoiGian:dd/MM HH:mm}" + (string.IsNullOrWhiteSpace(k.LyDo) ? "" : $": {k.LyDo}") + (string.IsNullOrWhiteSpace(k.MaTaiKhoan) ? "" : $" (khóa bởi {k.MaTaiKhoan})"),
                    ManHinhDich = "SO_DO_PHONG", HanhDong = "Xem phòng"
                });
            }
        }

        // Sự cố đang mở: mức cao/khẩn cấp hiện từng việc; còn lại chỉ cộng vào số đếm.
        var suCoMo = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => x.TrangThai != TrangThaiSuCo.DA_XU_LY && x.TrangThai != TrangThaiSuCo.DA_HUY)
            .Select(x => new { x.MaPhong, x.TieuDe, x.MucDo }).ToListAsync();
        dto.SuCoDangMo = suCoMo.Count;
        viec.AddRange(suCoMo.Where(x => x.MucDo is MucDoSuCo.CAO or MucDoSuCo.KHAN_CAP).OrderBy(x => x.MaPhong, StringComparer.Ordinal).Select(x => new FrontDeskTaskDto
        {
            Loai = "SU_CO_MO", MucDo = x.MucDo == MucDoSuCo.KHAN_CAP ? "NGHIEM_TRONG" : "CANH_BAO", MaThamChieu = x.MaPhong, MaPhong = x.MaPhong,
            MoTa = $"Sự cố phòng {x.MaPhong} ({(x.MucDo == MucDoSuCo.KHAN_CAP ? "khẩn cấp" : "mức cao")}): {x.TieuDe}",
            ManHinhDich = "BUONG_PHONG", HanhDong = "Mở Buồng phòng · sự cố"
        }));

        dto.MinibarChoTinhPhi = await _context.ChiTietKiemPhongs.AsNoTracking().CountAsync(x => x.TrangThaiTinhPhi == TrangThaiTinhPhi.CHO_TINH_PHI);
        if (dto.MinibarChoTinhPhi > 0)
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "MINIBAR_CHO_TINH_PHI", MucDo = "CANH_BAO", MaThamChieu = $"{dto.MinibarChoTinhPhi} khoản",
                MoTa = $"{dto.MinibarChoTinhPhi} khoản minibar buồng phòng ghi nhận đang chờ tính phí vào folio (chưa xử lý thì không trả phòng được)",
                ManHinhDich = "BUONG_PHONG", HanhDong = "Mở Buồng phòng · tính phí minibar"
            });

        dto.DoBoQuenDangLuu = await _context.DoBoQuens.AsNoTracking()
            .CountAsync(x => x.TrangThai == TrangThaiDoBoQuen.DA_TIM_THAY || x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU);
        if (dto.DoBoQuenDangLuu > 0)
            viec.Add(new FrontDeskTaskDto
            {
                Loai = "DO_BO_QUEN", MucDo = "THONG_TIN", MaThamChieu = $"{dto.DoBoQuenDangLuu} món",
                MoTa = $"{dto.DoBoQuenDangLuu} món đồ khách bỏ quên đang được lưu giữ, chưa trả khách",
                ManHinhDich = "BUONG_PHONG", HanhDong = "Mở Buồng phòng · đồ bỏ quên"
            });

        dto.BookingCanXuLy = bookingCanXuLy.Count;
        dto.ViecKhacCanXuLy = viec.Count(x => x.Loai is not ("CHO_COC" or "QUA_HAN_KHONG_DEN" or "PHONG_CHUA_SAN_SANG" or "HOA_DON_CON_NUA"));
        var thuTu = new[] { "PHONG_CHUA_SAN_SANG", "SU_CO_MO", "CHO_COC", "QUA_HAN_KHONG_DEN", "TRA_PHONG_QUA_HAN", "MINIBAR_CHO_TINH_PHI", "HOA_DON_CHUA_THU", "HOA_DON_CON_NUA", "PHONG_BAO_TRI", "DO_BO_QUEN" };
        dto.CanXuLy = viec.OrderBy(x => x.MucDo == "NGHIEM_TRONG" ? 0 : x.MucDo == "CANH_BAO" ? 1 : 2).ThenBy(x => Array.IndexOf(thuTu, x.Loai)).Take(SoViecToiDa).ToList();
        return dto;
    }

    private static string TenTinhTrang(TinhTrangPhong t) => t switch
    {
        TinhTrangPhong.VC => "trống, đã dọn",
        TinhTrangPhong.VD => "trống, chưa dọn",
        TinhTrangPhong.VI => "trống, đã kiểm tra",
        TinhTrangPhong.OC => "đang có khách",
        TinhTrangPhong.OD => "có khách, cần dọn",
        _ => "đang bảo trì"
    };

    public async Task<List<ArrivalDto>> GetArrivalsAsync(DateOnly? ngay)
    {
        var d = ngay ?? HomNay;
        var phieus = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.NgayDonDuKien == d && x.TrangThai != TrangThaiPhieuDat.DA_HUY && x.TrangThai != TrangThaiPhieuDat.KHONG_DEN)
            .OrderBy(x => x.Khach.HoTen)
            .Select(x => new
            {
                x.MaPhieuDat, x.MaKhach, x.Khach.HoTen, x.Khach.SoDT, x.TrangThai, x.LoaiDatPhong, x.NguonDat, x.NgayDonDuKien, x.NgayTraDuKien, x.TienCoc,
                SoKhachDiCung = x.KhachDiCungs.Count,
                Phongs = x.ChiTietPhieuDats.OrderBy(ct => ct.MaPhong).Select(ct => new { ct.MaPhong, ct.Phong.MaLoai, ct.Phong.TinhTrang }).ToList(),
                DaNhan = x.PhieuNhanPhong == null ? new List<string>() : x.PhieuNhanPhong.ChiTietPhieuNhans.Select(n => n.MaPhong).ToList()
            }).ToListAsync();

        return phieus.Select(x =>
        {
            var phongs = x.Phongs.Select(p => new ArrivalRoomDto
            {
                MaPhong = p.MaPhong, MaLoai = p.MaLoai, TinhTrang = p.TinhTrang.ToString(),
                DaNhan = x.DaNhan.Contains(p.MaPhong), SanSang = p.TinhTrang is TinhTrangPhong.VC or TinhTrangPhong.VI
            }).ToList();
            var chuaNhan = phongs.Where(p => !p.DaNhan).ToList();
            var daCoc = x.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN;
            return new ArrivalDto
            {
                MaPhieuDat = x.MaPhieuDat, MaKhach = x.MaKhach, HoTenKhach = x.HoTen, SoDTKhach = x.SoDT, TrangThai = x.TrangThai.ToString(),
                LoaiDatPhong = x.LoaiDatPhong.ToString(), NguonDat = x.NguonDat.ToString(), NgayDonDuKien = x.NgayDonDuKien, NgayTraDuKien = x.NgayTraDuKien,
                TienCoc = x.TienCoc, DaCoc = daCoc, SoKhachDiCung = x.SoKhachDiCung, Phongs = phongs,
                CoTheNhanNgay = x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN && chuaNhan.Count > 0 && chuaNhan.All(p => p.SanSang)
            };
        }).ToList();
    }

    public async Task<List<DepartureDto>> GetDeparturesAsync(DateOnly? ngay)
    {
        var d = ngay ?? HomNay;
        var homNay = HomNay;
        return await LayLuotOAsync(x => (x.TrangThai == DangO && (x.NgayTraDuKien == d || (d == homNay && x.QuaHan)))
                                        || (x.TrangThai == DaTra && x.NgayTra == d));
    }

    public Task<List<DepartureDto>> GetInHouseAsync() => LayLuotOAsync(x => x.TrangThai == DangO);

    private sealed record LuotO(string MaPhieuNhan, string MaPhong, string? MaPhieuDat, string? MaKhach, string? HoTen, string? SoDT,
        DateOnly NgayNhan, DateOnly? NgayTraDuKien, DateOnly? NgayTra, int SoNguoi, decimal DonGia, TrangThaiChiTietPhieuNhan TrangThai);

    // Tải các lượt lưu trú rồi lọc trong bộ nhớ (số phòng khách sạn nhỏ; tránh biểu thức LINQ phức tạp không dịch được sang SQL).
    private async Task<List<DepartureDto>> LayLuotOAsync(Func<DepartureDto, bool> loc)
    {
        var homNay = HomNay;
        var thang = homNay.AddDays(-45);
        var raw = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O || x.NgayTra >= thang)
            .Select(x => new LuotO(
                x.MaPhieuNhan, x.MaPhong, x.PhieuNhanPhong.MaPhieuDat,
                x.PhieuNhanPhong.MaKhach ?? (x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.MaKhach),
                x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen : x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen,
                x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.SoDT : x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.Khach.SoDT,
                x.NgayNhan,
                x.NgayTraDuKien ?? (x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.NgayTraDuKien),
                x.NgayTra, x.SoNguoi, x.DonGia, x.TrangThai))
            .ToListAsync();

        return raw.Select(x => new DepartureDto
            {
                MaPhieuNhan = x.MaPhieuNhan, MaPhong = x.MaPhong, MaPhieuDat = x.MaPhieuDat, MaKhach = x.MaKhach, HoTenKhach = x.HoTen, SoDTKhach = x.SoDT,
                NgayNhan = x.NgayNhan, NgayTraDuKien = x.NgayTraDuKien, NgayTra = x.NgayTra, SoNguoi = x.SoNguoi, DonGia = x.DonGia,
                TrangThai = x.TrangThai.ToString(),
                QuaHan = x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O && x.NgayTraDuKien is { } h && h < homNay
            })
            .Where(loc)
            .OrderBy(x => x.MaPhong, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<List<VacantRoomDto>> GetVacantRoomsAsync(DateOnly? nhan, DateOnly? tra, string? maLoai)
    {
        var homNay = HomNay;
        var tu = nhan ?? homNay;
        var den = tra ?? tu.AddDays(1);
        if (tu < homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_KHONG_HOP_LE", "Ngày nhận không được ở quá khứ.", "nhan");
        if (den <= tu)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày nhận.", "tra");
        if (den.DayNumber - tu.DayNumber > SoNgayToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Khoảng tìm phòng tối đa {SoNgayToiDa} đêm.", "tra");

        var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, tu, den);
        var q = _context.Phongs.AsNoTracking().Where(x => x.TinhTrang != TinhTrangPhong.OOO);
        if (!string.IsNullOrWhiteSpace(maLoai)) q = q.Where(x => x.MaLoai == maLoai.Trim());
        var phongs = await q.OrderBy(x => x.Tang).ThenBy(x => x.MaPhong)
            .Select(x => new { x.MaPhong, x.TenPhong, x.Tang, x.MaLoai, TenLoai = x.LoaiPhong.TenLoai, x.LoaiPhong.DonGia, x.TinhTrang }).ToListAsync();

        return phongs.Where(x => !daChiem.Contains(x.MaPhong)).Select(x => new VacantRoomDto
        {
            MaPhong = x.MaPhong, TenPhong = x.TenPhong, Tang = x.Tang, MaLoai = x.MaLoai, TenLoai = x.TenLoai, DonGia = x.DonGia, TinhTrang = x.TinhTrang.ToString(),
            NhanNgayDuoc = tu == homNay && x.TinhTrang is TinhTrangPhong.VC or TinhTrangPhong.VI
        }).ToList();
    }

    public async Task<NoShowReportDto> GetNoShowsAsync(DateOnly? tuNgay, DateOnly? denNgay)
    {
        var homNay = HomNay;
        var den = denNgay ?? homNay;
        var tu = tuNgay ?? den.AddDays(-SoNgayKhongDenMacDinh);
        if (den < tu)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "denNgay phải sau hoặc bằng tuNgay.", "denNgay");
        if (den.DayNumber - tu.DayNumber > 366)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", "Khoảng báo cáo tối đa 366 ngày.", "denNgay");

        var daDanhDau = await _bookings.SearchAsync(new BookingListQuery { TrangThai = nameof(TrangThaiPhieuDat.KHONG_DEN), LocTheoNgay = "NGAY_HUY", TuNgay = tu, DenNgay = den, PageSize = 100 });
        var choXuLy = await _bookings.SearchAsync(new BookingListQuery { QuaHanChuaNhan = true, PageSize = 100 });
        return new NoShowReportDto { TuNgay = tu, DenNgay = den, DaDanhDau = daDanhDau.Items, ChoXuLy = choXuLy.Items };
    }
}
