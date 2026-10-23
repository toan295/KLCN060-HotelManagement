using System.Globalization;
using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Middlewares;
using KLCN060.Infrastructure;

namespace KLCN060.Api.Services.Finance;

public interface IManagementReportExporter
{
    Task<(byte[] Noi, string TenFile, string LoaiNoiDung)> XuatAsync(DateOnly tu, DateOnly den, string? dinhDang, CurrentUser user);
}

/// <summary>
/// Xuất báo cáo quản trị (màn Báo cáo của Quản lý) ra Excel hoặc PDF: gom đúng các số liệu đang hiển thị trên màn hình (tổng quan, doanh thu, vận hành, đặt phòng,
/// công nợ, vấn đề cần chú ý, đối chiếu và cách tính) vào MỘT tài liệu có bố cục rõ ràng. Mỗi lần xuất được ghi nhật ký (ai xuất, kỳ nào, định dạng nào).
/// </summary>
public class ManagementReportExporter : IManagementReportExporter
{
    private readonly IManagementReportService _bc;
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public ManagementReportExporter(IManagementReportService baoCao, KLCN060DbContext context, IAuditTrail audit)
    {
        _bc = baoCao;
        _context = context;
        _audit = audit;
    }

    public async Task<(byte[] Noi, string TenFile, string LoaiNoiDung)> XuatAsync(DateOnly tu, DateOnly den, string? dinhDang, CurrentUser user)
    {
        var dd = dinhDang?.Trim().ToLowerInvariant();
        if (dd is not ("xlsx" or "pdf"))
            throw new ApiException(StatusCodes.Status400BadRequest, "DINH_DANG_KHONG_HOP_LE", "dinhDang chỉ nhận: xlsx, pdf.", "dinhDang");
        if (tu > den)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày không được lớn hơn đến ngày.", "from");
        if (den.DayNumber - tu.DayNumber + 1 > 366)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", "Khoảng báo cáo tối đa 366 ngày.", "to");

        var tongQuan = await _bc.GetOverviewAsync(tu, den, null);
        var doanhThu = await _bc.GetRevenueBreakdownAsync(tu, den);
        var vanHanh = await _bc.GetOperationsAsync(tu, den);
        var datPhong = await _bc.GetBookingReportAsync(tu, den);
        var congNo = await _bc.GetDebtsAsync();
        var vanDe = await _bc.GetInsightsAsync(tu, den);
        var doiChieu = await _bc.GetReconciliationAsync(tu, den);
        var dinhNghia = _bc.GetDefinitions();
        var tenNguoiXuat = await TenNguoiDung.HienThiAsync(_context, user.TenDN) ?? user.TenDN;

        var tl = new TaiLieuBaoCao
        {
            TieuDe = "BÁO CÁO QUẢN TRỊ",
            KyBaoCao = $"Kỳ báo cáo: {tu:dd/MM/yyyy} - {den:dd/MM/yyyy} ({tongQuan.KyNay.SoNgay} ngày)",
            SoSanh = $"So với kỳ trước: {tongQuan.KyTruoc.TuNgay:dd/MM/yyyy} - {tongQuan.KyTruoc.DenNgay:dd/MM/yyyy}",
            NguoiXuat = tenNguoiXuat,
            XuatLuc = DateTime.Now
        };
        tl.Phans.Add(PhanTongQuan(tongQuan));
        tl.Phans.Add(PhanDoanhThu(doanhThu));
        tl.Phans.Add(PhanVanHanh(vanHanh));
        tl.Phans.Add(PhanDatPhong(datPhong));
        tl.Phans.Add(PhanCongNo(congNo));
        tl.Phans.Add(PhanVanDe(vanDe));
        tl.Phans.Add(PhanDoiChieu(doiChieu, dinhNghia));

        var (noi, loai) = dd == "pdf"
            ? (ManagementReportWriter.ToPdf(tl), "application/pdf")
            : (ManagementReportWriter.ToXlsx(tl), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var tenFile = TenTepXuat.Tao("BaoCaoQuanTri", dd, $"{tu:dd.MM.yyyy}-{den:dd.MM.yyyy}");

        _audit.Ghi("XUAT_BAO_CAO_QUAN_TRI", $"{tu:yyyyMMdd}-{den:yyyyMMdd}", $"dinhDang={dd}; tuNgay={tu:yyyy-MM-dd}; denNgay={den:yyyy-MM-dd}; soPhan={tl.Phans.Count}");
        await _context.SaveChangesAsync();
        return (noi, tenFile, loai);
    }

    // =====================================================================================================
    // Tiện ích dựng bảng
    // =====================================================================================================

    private static CotBang C(string ten, KieuCot kieu = KieuCot.Chu) => new(ten, kieu);

    private static BangBaoCao Bang(string tieuDe, string phuDe, params CotBang[] cot) => new() { TieuDe = tieuDe, PhuDe = phuDe, Cot = cot.ToList() };

    private static string Pt(decimal v) => $"{v.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}%";

    private static string Thu(decimal? thayDoi) => thayDoi is null ? "Chưa có kỳ trước để so sánh" : $"{(thayDoi >= 0 ? "▲" : "▼")} {Math.Abs(thayDoi.Value).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}% so với kỳ trước";
    private static string Tone(decimal? thayDoi) => thayDoi is null ? "" : thayDoi >= 0 ? "tot" : "xau";
    private static string Dong(decimal v) => $"{Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ";

    /// <summary>Bảng "Nội dung - SL - Doanh thu - Tỷ trọng" cho một nhóm phân chia doanh thu; có dòng tổng.</summary>
    private static KhoiBang PhanChia(string tieuDe, string? ghiChu, List<RevenueSliceDto> ds, string tenSl = "Số lượng")
    {
        var b = Bang(tieuDe, ghiChu ?? "", C("Nội dung"), C(tenSl, KieuCot.So), C("Doanh thu (đ)", KieuCot.Tien), C("Tỷ trọng", KieuCot.So));
        foreach (var x in ds) b.ThemDong(x.Ten, x.SoLuong, x.DoanhThu, Pt(x.TyTrongPhanTram));
        if (ds.Count > 0) b.Tong = new object?[] { "TỔNG", ds.Sum(x => x.SoLuong), ds.Sum(x => x.DoanhThu), null };
        return new KhoiBang(b);
    }

    private static string TenTinhTrangPhong(string? ma) => ma switch
    {
        "VC" => "Trống, đã dọn", "VD" => "Trống, chưa dọn", "VI" => "Trống, đã kiểm tra", "OC" => "Đang có khách", "OD" => "Có khách, cần dọn", "OOO" => "Ngừng phục vụ", _ => ma ?? ""
    };

    private static string MucDo(string? m) => m switch { "NGHIEM_TRONG" => "NGHIÊM TRỌNG", "CANH_BAO" => "CẢNH BÁO", _ => "THÔNG TIN" };

    // =====================================================================================================
    // Các phần của báo cáo
    // =====================================================================================================

    private static PhanBaoCao PhanTongQuan(OverviewReportDto o)
    {
        var ky = o.KyNay; var kt = o.KyTruoc; var dl = o.ThayDoi;
        var khois = new List<KhoiBaoCao>
        {
            new KhoiThe(new List<TheSo>
            {
                new("Doanh thu (hóa đơn)", Dong(ky.DoanhThu), Thu(dl.DoanhThu), Tone(dl.DoanhThu)),
                new("Doanh thu phòng (theo đêm ở)", Dong(ky.DoanhThuPhong), Thu(dl.DoanhThuPhong), Tone(dl.DoanhThuPhong)),
                new("Công suất (Occupancy)", Pt(ky.CongSuatPhanTram), $"{Thu(dl.CongSuat)} · {ky.SoDemPhongBan:N0}/{ky.SoDemPhongKhaDung:N0} đêm phòng", Tone(dl.CongSuat)),
                new("ADR", Dong(ky.Adr), Thu(dl.Adr), Tone(dl.Adr)),
                new("RevPAR", Dong(ky.RevPar), Thu(dl.RevPar), Tone(dl.RevPar)),
                new("Đặt phòng mới", ky.SoDatPhongMoi.ToString("N0"), $"{Thu(dl.SoDatPhongMoi)} · hủy {Pt(ky.TyLeHuyPhanTram)} · không đến {Pt(ky.TyLeKhongDenPhanTram)}", Tone(dl.SoDatPhongMoi)),
                new("Check-in / Check-out", $"{ky.SoCheckIn:N0} / {ky.SoCheckOut:N0}", $"Hủy {ky.SoHuy} · không đến {ky.SoKhongDen}"),
                new("Doanh thu dịch vụ", Dong(ky.DoanhThuDichVu), "Tính trong doanh thu hóa đơn")
            })
        };

        var ss = Bang("So sánh với kỳ trước", $"Kỳ này {ky.TuNgay:dd/MM/yyyy}-{ky.DenNgay:dd/MM/yyyy} · kỳ trước {kt.TuNgay:dd/MM/yyyy}-{kt.DenNgay:dd/MM/yyyy}",
            C("Chỉ số"), C("Kỳ này", KieuCot.So), C("Kỳ trước", KieuCot.So), C("Thay đổi", KieuCot.So));
        void H(string ten, object? nay, object? truoc, decimal? thayDoi) => ss.ThemDong(ten, nay, truoc, thayDoi is null ? "—" : $"{(thayDoi >= 0 ? "+" : "-")}{Math.Abs(thayDoi.Value).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}%");
        H("Doanh thu (hóa đơn, đ)", ky.DoanhThu, kt.DoanhThu, dl.DoanhThu);
        H("Doanh thu phòng (đ)", ky.DoanhThuPhong, kt.DoanhThuPhong, dl.DoanhThuPhong);
        H("Doanh thu dịch vụ (đ)", ky.DoanhThuDichVu, kt.DoanhThuDichVu, null);
        H("Đêm phòng đã bán", ky.SoDemPhongBan, kt.SoDemPhongBan, null);
        H("Đêm phòng khả dụng", ky.SoDemPhongKhaDung, kt.SoDemPhongKhaDung, null);
        H("Công suất (%)", Pt(ky.CongSuatPhanTram), Pt(kt.CongSuatPhanTram), dl.CongSuat);
        H("ADR (đ)", ky.Adr, kt.Adr, dl.Adr);
        H("RevPAR (đ)", ky.RevPar, kt.RevPar, dl.RevPar);
        H("Đặt phòng mới", ky.SoDatPhongMoi, kt.SoDatPhongMoi, dl.SoDatPhongMoi);
        H("Hủy", ky.SoHuy, kt.SoHuy, null);
        H("Không đến", ky.SoKhongDen, kt.SoKhongDen, null);
        H("Check-in", ky.SoCheckIn, kt.SoCheckIn, null);
        H("Check-out", ky.SoCheckOut, kt.SoCheckOut, null);
        khois.Add(new KhoiBang(ss));

        var chuoi = Bang($"Diễn biến theo {(o.NhomTheo == "THANG" ? "tháng" : "ngày")} của kỳ này", "", C(o.NhomTheo == "THANG" ? "Tháng" : "Ngày"), C("Doanh thu (đ)", KieuCot.Tien), C("Doanh thu phòng (đ)", KieuCot.Tien),
            C("Công suất", KieuCot.So), C("ADR (đ)", KieuCot.Tien), C("RevPAR (đ)", KieuCot.Tien), C("Đặt mới", KieuCot.So));
        foreach (var p in o.ChuoiKyNay) chuoi.ThemDong(p.Nhan, p.DoanhThu, p.DoanhThuPhong, Pt(p.CongSuatPhanTram), p.Adr, p.RevPar, p.SoDatPhongMoi);
        if (o.ChuoiKyNay.Count > 0) chuoi.Tong = new object?[] { "TỔNG", o.ChuoiKyNay.Sum(x => x.DoanhThu), o.ChuoiKyNay.Sum(x => x.DoanhThuPhong), null, null, null, o.ChuoiKyNay.Sum(x => x.SoDatPhongMoi) };
        khois.Add(new KhoiBang(chuoi));
        return new PhanBaoCao("Tổng quan", "Tổng quan", "Công suất, ADR, RevPAR và doanh thu của kỳ so với kỳ liền trước cùng độ dài.", khois);
    }

    private static PhanBaoCao PhanDoanhThu(RevenueBreakdownDto r)
    {
        var khois = new List<KhoiBaoCao>
        {
            new KhoiThe(new List<TheSo> { new("Tổng doanh thu kỳ này", Dong(r.TongDoanhThu), "Doanh thu hóa đơn đã thu (gồm cả tiền cọc của phiếu đã thanh toán)") }),
            PhanChia("Theo loại doanh thu", "Phòng, dịch vụ, minibar, giặt là, khác và giảm giá (số âm); cộng lại bằng đúng tổng doanh thu.", r.TheoLoaiDoanhThu),
            PhanChia("Theo loại phòng", "Doanh thu phòng theo đêm ở.", r.TheoLoaiPhong, "Đêm phòng"),
            PhanChia("Theo nguồn đặt phòng", "Website, quầy lễ tân, vãng lai (không qua đặt phòng).", r.TheoNguonDat, "Đêm phòng"),
            PhanChia("Theo dịch vụ", "Theo ngày sử dụng dịch vụ.", r.TheoDichVu, "Lượt dùng"),
            PhanChia("Theo hình thức thanh toán (tiền thực thu, chưa gồm cọc)", null, r.TheoHinhThucThanhToan, "Số lần"),
            PhanChia("Theo hình thức thanh toán (gồm cả tiền cọc đã nhận)", "Số liệu dùng đối chiếu quỹ và ngân hàng.", r.TheoHinhThucGomCoc, "Số lần"),
            PhanChia("Theo tuần", "Thứ Hai - Chủ nhật của ngày lập hóa đơn.", r.TheoTuan),
            PhanChia("Theo tháng", null, r.TheoThang),
            PhanChia("Theo quý", null, r.TheoQuy),
            PhanChia("Theo năm", null, r.TheoNam),
            PhanChia("Doanh thu phòng theo tầng", null, r.TheoTang, "Đêm phòng"),
            PhanChia("Doanh thu phòng theo từng phòng", null, r.TheoPhong, "Đêm phòng")
        };
        var ngay = Bang("Theo ngày", "", C("Ngày", KieuCot.Ngay), C("Doanh thu (đ)", KieuCot.Tien));
        foreach (var d in r.TheoNgay) ngay.ThemDong(d.Ngay, d.DoanhThu);
        if (r.TheoNgay.Count > 0) ngay.Tong = new object?[] { "TỔNG", r.TheoNgay.Sum(x => x.DoanhThu) };
        khois.Add(new KhoiBang(ngay));
        return new PhanBaoCao("Doanh thu", "Doanh thu", "Doanh thu theo nhiều chiều: loại, loại phòng, nguồn đặt, dịch vụ, hình thức thanh toán và thời gian.", khois);
    }

    private static PhanBaoCao PhanVanHanh(OperationsReportDto v)
    {
        var lt = Bang("Lễ tân (Front Office)", "", C("Chỉ số"), C("Giá trị", KieuCot.So));
        lt.ThemDong("Check-in", v.LeTan.CheckIn); lt.ThemDong("Check-out", v.LeTan.CheckOut); lt.ThemDong("Khách vãng lai (walk-in)", v.LeTan.KhachVangLai);
        lt.ThemDong("Đặt phòng mới", v.LeTan.DatPhongMoi); lt.ThemDong("Không đến (no-show)", v.LeTan.NoShow); lt.ThemDong("Hủy đặt phòng", v.LeTan.Huy);
        lt.ThemDong("Công suất trung bình", Pt(v.LeTan.CongSuatTrungBinhPhanTram));

        var kt = Bang("Kế toán (Accounting)", "", C("Chỉ số"), C("Giá trị (đ)", KieuCot.Tien), C("Số lần / số hóa đơn", KieuCot.So));
        var k = v.KeToan;
        kt.ThemDong("Doanh thu", k.DoanhThu, null);
        kt.ThemDong("Thu tiền trong kỳ", k.ThanhToan, k.SoLanThanhToan);
        kt.ThemDong("Công nợ hiện tại", k.CongNoHienTai, k.SoHoaDonConNo);
        kt.ThemDong("Hoàn cọc", k.HoanTien, k.SoLanHoanTien);
        kt.ThemDong("Cọc giữ lại (hủy / không đến)", k.CocGiuLai, null);
        var khois = new List<KhoiBaoCao> { new KhoiBang(lt), new KhoiBang(kt) };

        var bp = Bang("Buồng phòng (Housekeeping)", $"Phòng đã dọn trong kỳ: {v.BuongPhong.PhongDaDon} · đang chờ dọn: {v.BuongPhong.PhongDangCho} · có vấn đề: {v.BuongPhong.PhongCoVanDe}",
            C("Phòng"), C("Tình trạng"), C("Từ lúc", KieuCot.NgayGio), C("Số giờ", KieuCot.So), C("Lý do"));
        foreach (var p in v.BuongPhong.DanhSachCanXuLy) bp.ThemDong(p.MaPhong, TenTinhTrangPhong(p.TinhTrang), p.TuLuc, p.SoGio is { } g ? Math.Round(g, 1) : null, p.LyDo ?? "");
        khois.Add(new KhoiBang(bp));

        var bt = Bang("Bảo trì (Maintenance)", $"Sự cố trong kỳ: {v.BaoTri.SoSuCo} · đang xử lý: {v.BaoTri.DangXuLy} · đã xử lý: {v.BaoTri.DaXuLy} · thời gian xử lý trung bình: {(v.BaoTri.ThoiGianXuLyTrungBinhGio is { } h ? $"{h:0.#} giờ" : "chưa có")}",
            C("Phòng đang bảo trì"), C("Từ lúc", KieuCot.NgayGio), C("Số giờ", KieuCot.So), C("Lý do"));
        foreach (var p in v.BaoTri.PhongDangBaoTri) bt.ThemDong(p.MaPhong, p.TuLuc, p.SoGio is { } g ? Math.Round(g, 1) : null, p.LyDo ?? "");
        khois.Add(new KhoiBang(bt));
        return new PhanBaoCao("Vận hành", "Vận hành các bộ phận", "Lễ tân, kế toán, buồng phòng và bảo trì trong kỳ.", khois);
    }

    private static PhanBaoCao PhanDatPhong(BookingReportDto d)
    {
        var khois = new List<KhoiBaoCao>
        {
            new KhoiThe(new List<TheSo>
            {
                new("Tổng số phiếu", d.TongSoPhieu.ToString("N0")),
                new("Tỷ lệ hủy", Pt(d.TyLeHuyPhanTram), null, d.TyLeHuyPhanTram >= 20 ? "xau" : ""),
                new("Tỷ lệ không đến", Pt(d.TyLeKhongDenPhanTram), null, d.TyLeKhongDenPhanTram >= 10 ? "xau" : ""),
                new("Đặt phòng đoàn", d.SoPhieuDoan.ToString("N0"), $"{d.SoPhongDoan} phòng"),
                new("Cọc đã hoàn", Dong(d.TongCocHoan)),
                new("Cọc giữ lại", Dong(d.TongCocGiuLai)),
                new("Phiếu quá ngày đón chưa xử lý", d.ChoXuLyKhongDen.ToString("N0"), "Chờ đánh dấu không đến", d.ChoXuLyKhongDen > 0 ? "xau" : "tot")
            })
        };
        KhoiBang Dem(string ten, List<BookingCountDto> ds)
        {
            var b = Bang(ten, "", C("Nội dung"), C("Số phiếu", KieuCot.So), C("Giá trị (đ)", KieuCot.Tien));
            foreach (var x in ds) b.ThemDong(x.Ten, x.SoLuong, x.GiaTri);
            if (ds.Count > 0) b.Tong = new object?[] { "TỔNG", ds.Sum(x => x.SoLuong), ds.Sum(x => x.GiaTri) };
            return new KhoiBang(b);
        }
        khois.Add(Dem("Theo trạng thái", d.TheoTrangThai));
        khois.Add(Dem("Theo nguồn đặt", d.TheoNguon));
        khois.Add(Dem("Cá nhân / đoàn", d.TheoLoai));

        var huy = Bang("Hủy / không đến gần đây (trong kỳ)", "Người hủy hiển thị họ tên và mã nhân viên.", C("Phiếu"), C("Khách"), C("Loại"), C("Ngày", KieuCot.NgayGio), C("Người hủy"), C("Lý do"), C("Cọc (đ)", KieuCot.Tien), C("Hoàn (đ)", KieuCot.Tien));
        foreach (var h in d.HuyGanDay)
            huy.ThemDong(h.MaPhieuDat, h.HoTenKhach, h.TrangThai == "KHONG_DEN" ? "Không đến" : "Đã hủy", h.NgayHuy, h.HuyBoi ?? "", h.LyDoHuy ?? "", h.TienCoc, h.SoTienHoanCoc);
        if (d.HuyGanDay.Count > 0) huy.Tong = new object?[] { "TỔNG", null, null, null, null, null, d.HuyGanDay.Sum(x => x.TienCoc), d.HuyGanDay.Sum(x => x.SoTienHoanCoc) };
        khois.Add(new KhoiBang(huy));
        return new PhanBaoCao("Đặt phòng", "Đặt phòng", "Phiếu đặt theo trạng thái, nguồn, loại (đoàn), tỷ lệ hủy / không đến, hoàn và giữ cọc.", khois);
    }

    private static PhanBaoCao PhanCongNo(DebtReportDto c)
    {
        var khois = new List<KhoiBaoCao>
        {
            new KhoiThe(new List<TheSo> { new("Tổng công nợ hiện tại", Dong(c.TongConNo), $"{c.SoHoaDon} hóa đơn chưa thanh toán đủ", c.TongConNo > 0 ? "xau" : "tot") }),
            PhanChia("Theo tuổi nợ", "Công nợ không phụ thuộc kỳ báo cáo: tính tại thời điểm xuất.", c.TheoNhomTuoiNo, "Số hóa đơn")
        };
        var kh = Bang("Công nợ theo khách", "", C("Khách"), C("Số hóa đơn", KieuCot.So), C("Nợ lâu nhất (ngày)", KieuCot.So), C("Còn nợ (đ)", KieuCot.Tien));
        foreach (var x in c.TheoKhach) kh.ThemDong(x.HoTenKhach ?? x.Ma, x.SoHoaDon, x.SoNgayNoLonNhat, x.ConNo);
        if (c.TheoKhach.Count > 0) kh.Tong = new object?[] { "TỔNG", c.TheoKhach.Sum(x => x.SoHoaDon), null, c.TheoKhach.Sum(x => x.ConNo) };
        khois.Add(new KhoiBang(kh));

        var dp = Bang("Booking còn công nợ", "", C("Booking"), C("Khách"), C("Số hóa đơn", KieuCot.So), C("Nợ lâu nhất (ngày)", KieuCot.So), C("Còn nợ (đ)", KieuCot.Tien));
        foreach (var x in c.TheoDatPhong) dp.ThemDong(x.Ma, x.HoTenKhach ?? "", x.SoHoaDon, x.SoNgayNoLonNhat, x.ConNo);
        if (c.TheoDatPhong.Count > 0) dp.Tong = new object?[] { "TỔNG", null, c.TheoDatPhong.Sum(x => x.SoHoaDon), null, c.TheoDatPhong.Sum(x => x.ConNo) };
        khois.Add(new KhoiBang(dp));

        var ct = Bang("Chi tiết hóa đơn còn nợ", "", C("Hóa đơn"), C("Khách"), C("Booking"), C("Ngày lập", KieuCot.NgayGio), C("Số ngày nợ", KieuCot.So), C("Tổng tiền (đ)", KieuCot.Tien), C("Đã thu (đ)", KieuCot.Tien), C("Còn nợ (đ)", KieuCot.Tien), C("Nhóm tuổi nợ"));
        foreach (var x in c.ChiTiet) ct.ThemDong(x.MaHD, x.HoTenKhach ?? "", x.MaPhieuDat ?? "", x.NgayLap, x.SoNgayNo, x.TongTien, x.DaThanhToan, x.ConNo, x.NhomTuoiNo);
        if (c.ChiTiet.Count > 0) ct.Tong = new object?[] { "TỔNG", null, null, null, null, c.ChiTiet.Sum(x => x.TongTien), c.ChiTiet.Sum(x => x.DaThanhToan), c.ChiTiet.Sum(x => x.ConNo), null };
        khois.Add(new KhoiBang(ct));
        return new PhanBaoCao("Công nợ", "Công nợ", "Hóa đơn còn nợ theo tuổi nợ, theo khách và theo booking.", khois);
    }

    private static PhanBaoCao PhanVanDe(ReportInsightsDto v)
    {
        var khois = new List<KhoiBaoCao>();
        var nghiemTrong = v.Items.Count(x => x.MucDo == "NGHIEM_TRONG"); var canhBao = v.Items.Count(x => x.MucDo == "CANH_BAO");
        khois.Add(new KhoiThe(new List<TheSo>
        {
            new("Tổng số vấn đề", v.Items.Count.ToString("N0"), v.Items.Count == 0 ? "Không có vấn đề nào cần chú ý" : $"{nghiemTrong} nghiêm trọng · {canhBao} cảnh báo · {v.Items.Count - nghiemTrong - canhBao} thông tin", v.Items.Count == 0 ? "tot" : nghiemTrong > 0 ? "xau" : "")
        }));
        var b = Bang("Danh sách vấn đề và cách xử lý", "Sắp theo mức độ nghiêm trọng.", C("Mức độ"), C("Vấn đề"), C("Số liệu"), C("Mô tả"), C("Hành động đề xuất"));
        foreach (var x in v.Items.OrderBy(x => x.MucDo == "NGHIEM_TRONG" ? 0 : x.MucDo == "CANH_BAO" ? 1 : 2))
            b.ThemDong(MucDo(x.MucDo), x.TieuDe, x.SoLieu, x.MoTa, x.HanhDong);
        khois.Add(new KhoiBang(b));
        var chiTiet = v.Items.Where(x => x.ChiTiet.Count > 0).ToList();
        if (chiTiet.Count > 0)
        {
            var d = Bang("Chi tiết từng vấn đề", "", C("Vấn đề"), C("Chi tiết"));
            foreach (var x in chiTiet) foreach (var dong in x.ChiTiet) d.ThemDong(x.TieuDe, dong);
            khois.Add(new KhoiBang(d));
        }
        return new PhanBaoCao("Vấn đề cần chú ý", "Vấn đề cần chú ý", "Các điểm bất thường trong kỳ kèm gợi ý hành động.", khois);
    }

    private static PhanBaoCao PhanDoiChieu(ReportReconciliationDto r, List<ReportDefinitionDto> dinhNghia)
    {
        var khois = new List<KhoiBaoCao>();
        var lech = r.KiemTra.Count(x => !x.Khop);
        khois.Add(new KhoiThe(new List<TheSo>
        {
            new("Kết quả kiểm tra nhất quán", r.KiemTra.Count == 0 ? "—" : lech == 0 ? $"{r.KiemTra.Count}/{r.KiemTra.Count} khớp" : $"{lech}/{r.KiemTra.Count} LỆCH", "Tổng = chi tiết", lech == 0 ? "tot" : "xau"),
            new("Doanh thu hóa đơn", Dong(r.DoanhThuHoaDon), "Theo ngày lập hóa đơn"),
            new("Doanh thu phòng theo đêm ở", Dong(r.DoanhThuPhongTheoDem), $"{r.SoDemPhongBan:N0} đêm phòng đã bán")
        }));
        if (!string.IsNullOrWhiteSpace(r.GiaiThich)) khois.Add(new KhoiGhiChu("Vì sao doanh thu hóa đơn khác doanh thu phòng theo đêm ở?", new[] { r.GiaiThich }));
        var kt = Bang("Các phép kiểm tra nhất quán", "", C("Phép kiểm tra"), C("Vế trái"), C("Vế phải"), C("Kết quả"), C("Ghi chú"));
        foreach (var x in r.KiemTra) kt.ThemDong(x.Ten, x.VeTrai, x.VePhai, x.Khop ? "Khớp" : "LỆCH", x.GhiChu);
        khois.Add(new KhoiBang(kt));
        var dn = Bang("Cách tính các chỉ số", "", C("Chỉ số"), C("Công thức"), C("Nguồn dữ liệu"), C("Ghi chú"));
        foreach (var x in dinhNghia) dn.ThemDong(x.Ten, x.CongThuc, x.NguonDuLieu, x.GhiChu);
        khois.Add(new KhoiBang(dn));
        return new PhanBaoCao("Đối chiếu và cách tính", "Đối chiếu và cách tính", "Số liệu được kiểm tra chéo và định nghĩa từng chỉ số để người đọc tự kiểm chứng.", khois);
    }
}
