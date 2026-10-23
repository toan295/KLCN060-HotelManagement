using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class ManagementReportService
{
    // Ngưỡng cảnh báo (đưa vào kết quả để người xem biết "bao nhiêu thì bị coi là vấn đề").
    public const decimal NguongCongSuatThap = 40m;
    public const decimal NguongCongSuatRatThap = 25m;
    public const decimal NguongTyLeHuy = 20m;
    public const decimal NguongTyLeKhongDen = 5m;
    public const int SoGioChoXacNhan = 24;
    public const int SoNgaySuCoQuaHan = 3;
    public const decimal NguongChuaGhiNhan = 50m;

    private static string Vnd(decimal x) => $"{Math.Round(x, 0).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}đ";
    private static string Pct(decimal x) => $"{x.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}%";

    // =====================================================================================================
    // Từ điển chỉ số
    // =====================================================================================================

    public List<ReportDefinitionDto> GetDefinitions() => new()
    {
        new() { Ma = "DOANH_THU", Ten = "Doanh thu (hóa đơn)", CongThuc = "Σ (Tổng tiền hóa đơn + Tiền cọc đã trừ) của các hóa đơn LẬP trong kỳ và đã thu được ít nhất một phần",
            NguonDuLieu = "Hóa đơn", GhiChu = "Tính theo NGÀY LẬP hóa đơn (không theo đêm ở). Hóa đơn chưa thu đồng nào chưa được tính; khuyến mãi / giảm giá đã duyệt đã được trừ." },
        new() { Ma = "DOANH_THU_PHONG", Ten = "Doanh thu phòng (theo đêm ở)", CongThuc = "Σ giá của từng đêm phòng đã bán trong kỳ",
            NguonDuLieu = "Lượt lưu trú + giá chốt từng đêm của phiếu đặt", GhiChu = "Tính theo ĐÊM Ở thực tế, kể cả khách đang ở hoặc chưa thanh toán nên có thể lớn hơn doanh thu hóa đơn (xem mục Đối chiếu doanh thu). Đây là cơ sở của ADR và RevPAR." },
        new() { Ma = "DEM_PHONG_BAN", Ten = "Đêm phòng đã bán (Room Nights)", CongThuc = "Số cặp (phòng, đêm) có khách ở trong kỳ",
            NguonDuLieu = "Lượt lưu trú", GhiChu = "Lượt ở chiếm các đêm [ngày nhận, ngày trả); đang ở tính tới hết hôm nay; nhận-trả cùng ngày tính 1 đêm; một phòng chỉ bán tối đa 1 đêm/ngày." },
        new() { Ma = "DEM_PHONG_KHA_DUNG", Ten = "Đêm phòng khả dụng", CongThuc = "Tổng số phòng × số ngày của kỳ",
            NguonDuLieu = "Danh mục phòng", GhiChu = "Gồm cả phòng đang bảo trì (không loại trừ OOO), nên công suất phản ánh cả thiệt hại do bảo trì." },
        new() { Ma = "CONG_SUAT", Ten = "Công suất (Occupancy)", CongThuc = "Đêm phòng đã bán ÷ Đêm phòng khả dụng × 100%", NguonDuLieu = "Đêm phòng", GhiChu = "Công suất tức thời trên bảng điều hành = phòng đang có khách ÷ tổng số phòng tại thời điểm xem." },
        new() { Ma = "ADR", Ten = "ADR (giá phòng bình quân)", CongThuc = "Doanh thu phòng ÷ Đêm phòng đã bán", NguonDuLieu = "Đêm phòng", GhiChu = "Bằng 0 khi chưa bán đêm nào. Làm tròn đến đồng." },
        new() { Ma = "REVPAR", Ten = "RevPAR", CongThuc = "Doanh thu phòng ÷ Đêm phòng khả dụng  (= ADR × Công suất)", NguonDuLieu = "Đêm phòng", GhiChu = "Làm tròn đến đồng." },
        new() { Ma = "DAT_PHONG_MOI", Ten = "Đặt phòng mới", CongThuc = "Số phiếu đặt phòng có NGÀY ĐẶT trong kỳ", NguonDuLieu = "Phiếu đặt phòng", GhiChu = "Gồm cả phiếu về sau bị hủy / không đến." },
        new() { Ma = "TY_LE_HUY", Ten = "Tỷ lệ hủy", CongThuc = "Số phiếu đặt trong kỳ hiện đã hủy ÷ Số phiếu đặt trong kỳ × 100%", NguonDuLieu = "Phiếu đặt phòng", GhiChu = "Theo nhóm phiếu đặt trong kỳ (không theo ngày hủy). 'Số hủy' trong kỳ thì đếm theo ngày hủy." },
        new() { Ma = "TY_LE_KHONG_DEN", Ten = "Tỷ lệ không đến (No-show)", CongThuc = "Số phiếu đặt trong kỳ ở trạng thái 'khách không đến' ÷ Số phiếu đặt trong kỳ × 100%", NguonDuLieu = "Phiếu đặt phòng", GhiChu = "Phiếu quá ngày đón mà chưa xử lý được liệt kê riêng ở tab Đặt phòng." },
        new() { Ma = "CHECK_IN_OUT", Ten = "Check-in / Check-out", CongThuc = "Số phiếu nhận phòng có ngày nhận trong kỳ / Số lượt phòng đã trả có ngày trả trong kỳ", NguonDuLieu = "Phiếu nhận phòng", GhiChu = "Check-out đếm theo phòng." },
        new() { Ma = "CONG_NO", Ten = "Công nợ", CongThuc = "Σ (Tổng tiền hóa đơn − Tiền đã thanh toán) của các hóa đơn còn nợ tại thời điểm xem", NguonDuLieu = "Hóa đơn + thanh toán", GhiChu = $"Quá hạn khi hóa đơn lập quá {SoNgayCongNoQuaHan} ngày. Không phụ thuộc kỳ báo cáo." },
        new() { Ma = "SO_SANH_KY_TRUOC", Ten = "So với kỳ trước", CongThuc = "(Kỳ này − Kỳ trước) ÷ Kỳ trước × 100%", NguonDuLieu = "Kỳ liền trước, dài bằng kỳ này", GhiChu = "Không hiển thị khi kỳ trước bằng 0 (không có cơ sở so sánh)." },
    };

    // =====================================================================================================
    // Đối chiếu doanh thu + kiểm tra nhất quán
    // =====================================================================================================

    /// <summary>Đối chiếu doanh thu hóa đơn ↔ doanh thu phòng theo đêm (nhẹ, không kèm các phép kiểm tra nhất quán).</summary>
    private async Task<ReportReconciliationDto> TinhDoiChieuAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var dem = await LayDemPhongAsync(from, to);
        var hoaDons = await LayHoaDonAsync(from, to);
        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var phongHoaDon = (await Finance.InvoiceRevenue.TaiAsync(_context, tuLuc, denLuc)).Sum(x => x.Phong);

        var coHoaDon = (await _context.HoaDons.AsNoTracking().Where(x => x.TrangThaiThanhToan != TrangThaiThanhToan.CHUA_THANH_TOAN)
            .Select(x => x.MaPhieuNhan).Distinct().ToListAsync()).ToHashSet();
        var daCo = dem.Where(x => x.MaPhieuNhan != null && coHoaDon.Contains(x.MaPhieuNhan)).ToList();
        var chua = dem.Where(x => x.MaPhieuNhan == null || !coHoaDon.Contains(x.MaPhieuNhan)).ToList();

        var tongPhong = await TongPhongAsync();
        var kpi = TinhKpi(dem, KhaDung(tongPhong, _ngayTaoPhong, from, to.DayNumber - from.DayNumber + 1));
        var tongHd = hoaDons.Sum(x => x.DoanhThu);
        var dto = new ReportReconciliationDto
        {
            TuNgay = from, DenNgay = to, DoanhThuHoaDon = tongHd, DoanhThuHoaDonPhong = phongHoaDon, DoanhThuPhongTheoDem = kpi.DoanhThuPhong, SoDemPhongBan = dem.Count,
            SoDemDaCoHoaDon = daCo.Count, TienDemDaCoHoaDon = daCo.Sum(x => x.DonGia), SoDemChuaCoHoaDon = chua.Count, TienDemChuaCoHoaDon = chua.Sum(x => x.DonGia)
        };
        dto.GiaiThich = $"Doanh thu hóa đơn {Vnd(tongHd)} (theo ngày lập hóa đơn, đã thu) khác doanh thu phòng theo đêm {Vnd(kpi.DoanhThuPhong)} vì: " +
            $"{chua.Count} đêm ({Vnd(dto.TienDemChuaCoHoaDon)}) thuộc lượt ở chưa có hóa đơn đã thu (khách đang ở / chưa trả phòng / chưa thanh toán) nên chưa vào doanh thu hóa đơn; " +
            $"hóa đơn lập trong kỳ có thể gồm đêm ở của kỳ trước; hóa đơn còn có dịch vụ, phụ thu và trừ khuyến mãi / giảm giá.";

        return dto;
    }

    public async Task<ReportReconciliationDto> GetReconciliationAsync(DateOnly from, DateOnly to)
    {
        var dto = await TinhDoiChieuAsync(from, to);

        // ---- kiểm tra nhất quán giữa các số
        var pb = await GetRevenueBreakdownAsync(from, to);
        var ov = await GetOverviewAsync(from, to, "NGAY");
        var no = await GetDebtsAsync();
        decimal Tong(IEnumerable<RevenueSliceDto> s) => s.Sum(x => x.DoanhThu);
        var kiemTra = new List<ReportCheckDto>();
        void Them(string ten, decimal a, decimal b, string ghiChu = "", decimal dungSai = 0)
            => kiemTra.Add(new ReportCheckDto { Ten = ten, VeTrai = Vnd(a), VePhai = Vnd(b), Khop = Math.Abs(a - b) <= dungSai, GhiChu = ghiChu });
        Them("Tổng doanh thu = tổng theo ngày", pb.TongDoanhThu, pb.TheoNgay.Sum(x => x.DoanhThu));
        Them("Tổng doanh thu = tổng theo loại doanh thu", pb.TongDoanhThu, Tong(pb.TheoLoaiDoanhThu));
        Them("Tổng doanh thu = tổng theo tháng", pb.TongDoanhThu, Tong(pb.TheoThang));
        Them("Tổng doanh thu (breakdown) = KPI doanh thu", pb.TongDoanhThu, ov.KyNay.DoanhThu);
        Them("Tổng chuỗi biểu đồ = KPI doanh thu", ov.ChuoiKyNay.Sum(x => x.DoanhThu), ov.KyNay.DoanhThu);
        Them("Doanh thu phòng theo loại phòng = KPI", Tong(pb.TheoLoaiPhong), ov.KyNay.DoanhThuPhong);
        Them("Doanh thu phòng theo nguồn đặt = KPI", Tong(pb.TheoNguonDat), ov.KyNay.DoanhThuPhong);
        Them("Doanh thu phòng theo tầng = KPI", Tong(pb.TheoTang), ov.KyNay.DoanhThuPhong);
        Them("Doanh thu phòng theo từng phòng = KPI", Tong(pb.TheoPhong), ov.KyNay.DoanhThuPhong);
        Them("ADR × đêm bán ≈ doanh thu phòng", ov.KyNay.Adr * ov.KyNay.SoDemPhongBan, ov.KyNay.DoanhThuPhong, "Lệch tối đa 0,5đ mỗi đêm do làm tròn ADR", ov.KyNay.SoDemPhongBan * 0.5m);
        Them("RevPAR × đêm khả dụng ≈ doanh thu phòng", ov.KyNay.RevPar * ov.KyNay.SoDemPhongKhaDung, ov.KyNay.DoanhThuPhong, "Lệch tối đa 0,5đ mỗi đêm khả dụng do làm tròn RevPAR", ov.KyNay.SoDemPhongKhaDung * 0.5m);
        Them("Công suất × đêm khả dụng ≈ đêm đã bán", Math.Round(ov.KyNay.CongSuatPhanTram * ov.KyNay.SoDemPhongKhaDung / 100m, 0), ov.KyNay.SoDemPhongBan, "Lệch do làm tròn công suất 2 chữ số", ov.KyNay.SoDemPhongKhaDung * 0.005m + 1);
        kiemTra.Add(new ReportCheckDto { Ten = "Công nợ: tổng = theo tuổi nợ = theo hóa đơn", VeTrai = Vnd(no.TongConNo), VePhai = Vnd(Tong(no.TheoNhomTuoiNo)), Khop = no.TongConNo == Tong(no.TheoNhomTuoiNo) && no.TongConNo == no.ChiTiet.Sum(x => x.ConNo) });
        dto.KiemTra = kiemTra;
        return dto;
    }

    // =====================================================================================================
    // Vấn đề cần chú ý
    // =====================================================================================================

    public async Task<ReportInsightsDto> GetInsightsAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var tongPhong = await TongPhongAsync();
        var kpi = await TinhKyAsync(from, to, tongPhong);
        var items = new List<ReportInsightDto>();
        void Them(string ma, string mucDo, string tieuDe, string moTa, string soLieu, string hanhDong, string manHinh, IEnumerable<string>? chiTiet = null)
            => items.Add(new ReportInsightDto { Ma = ma, MucDo = mucDo, TieuDe = tieuDe, MoTa = moTa, SoLieu = soLieu, HanhDong = hanhDong, ManHinh = manHinh, ChiTiet = (chiTiet ?? Enumerable.Empty<string>()).Take(8).ToList() });

        // 1. Công suất kỳ báo cáo thấp
        if (kpi.SoDemPhongKhaDung > 0 && kpi.CongSuatPhanTram < NguongCongSuatThap)
            Them("CONG_SUAT_THAP", kpi.CongSuatPhanTram < NguongCongSuatRatThap ? "NGHIEM_TRONG" : "CANH_BAO", "Công suất phòng thấp",
                $"Trong kỳ chỉ bán {kpi.SoDemPhongBan:N0}/{kpi.SoDemPhongKhaDung:N0} đêm phòng.", $"{Pct(kpi.CongSuatPhanTram)} (ngưỡng {Pct(NguongCongSuatThap)})",
                "Xem lại bảng giá, khuyến mãi và nguồn đặt phòng.", "DAT_PHONG");

        // 2. Công suất 7 ngày tới (đã giữ phòng)
        var tuNay = homNay; var den7 = homNay.AddDays(6);
        var demSapToi = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(x => (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                && x.PhieuDatPhong.NgayDonDuKien <= den7 && x.PhieuDatPhong.NgayTraDuKien > tuNay)
            .Select(x => new { x.PhieuDatPhong.NgayDonDuKien, x.PhieuDatPhong.NgayTraDuKien }).ToListAsync();
        var daBan7 = demSapToi.Sum(x =>
        {
            var tu = x.NgayDonDuKien > tuNay ? x.NgayDonDuKien : tuNay;
            var cuoi = x.NgayTraDuKien.AddDays(-1) < den7 ? x.NgayTraDuKien.AddDays(-1) : den7;
            return Math.Max(0, cuoi.DayNumber - tu.DayNumber + 1);
        });
        var inHouse = await _context.ChiTietPhieuNhans.AsNoTracking().Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => new { x.NgayTraDuKien }).ToListAsync();
        daBan7 += inHouse.Sum(x => Math.Clamp(((x.NgayTraDuKien ?? den7.AddDays(1)).DayNumber - tuNay.DayNumber), 0, 7));
        var khaDung7 = tongPhong * 7;
        var congSuat7 = PhanTram(daBan7, khaDung7);
        if (khaDung7 > 0 && congSuat7 < 30m)
            Them("CONG_SUAT_7_NGAY_TOI", congSuat7 < 15m ? "NGHIEM_TRONG" : "CANH_BAO", "7 ngày tới còn nhiều phòng trống",
                $"Đã giữ phòng (đang ở + đặt trước) cho {daBan7}/{khaDung7} đêm phòng của 7 ngày tới.", $"{Pct(congSuat7)} (ngưỡng 30%)", "Cân nhắc khuyến mãi / điều chỉnh giá cho các ngày còn trống.", "DAT_PHONG");

        // 3. Booking chưa xác nhận (chưa thu cọc)
        var choXn = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN && x.NgayDonDuKien >= homNay)
            .Select(x => new { x.MaPhieuDat, x.NgayDat, x.NgayDonDuKien, x.Khach.HoTen }).ToListAsync();
        var choLau = choXn.Where(x => bayGio - x.NgayDat > TimeSpan.FromHours(SoGioChoXacNhan)).OrderBy(x => x.NgayDat).ToList();
        if (choXn.Count > 0)
            Them("BOOKING_CHUA_XAC_NHAN", choLau.Count > 0 ? "CANH_BAO" : "THONG_TIN", "Đặt phòng chưa xác nhận (chưa thu cọc)",
                $"{choXn.Count} phiếu đang giữ phòng nhưng chưa xác nhận cọc; {choLau.Count} phiếu chờ quá {SoGioChoXacNhan} giờ.", $"{choXn.Count} phiếu ({choLau.Count} quá {SoGioChoXacNhan}h)",
                "Liên hệ khách thu cọc hoặc hủy để nhả phòng.", "DAT_PHONG", choLau.Select(x => $"{x.MaPhieuDat} - {x.HoTen} - đến {x.NgayDonDuKien:dd/MM}, chờ {(int)(bayGio - x.NgayDat).TotalHours} giờ"));

        // 4. Phòng chưa sẵn sàng cho khách đến hôm nay
        var denHomNay = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(x => x.PhieuDatPhong.NgayDonDuKien == homNay && x.PhieuDatPhong.PhieuNhanPhong == null
                && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
                && x.Phong.TinhTrang != TinhTrangPhong.VC && x.Phong.TinhTrang != TinhTrangPhong.VI)
            .Select(x => new { x.MaPhong, x.PhieuDatPhong.MaPhieuDat, TinhTrang = x.Phong.TinhTrang }).ToListAsync();
        if (denHomNay.Count > 0)
            Them("PHONG_CHUA_SAN_SANG", "NGHIEM_TRONG", "Phòng cho khách đến hôm nay chưa sẵn sàng",
                $"{denHomNay.Count} phòng đã giữ cho khách đến hôm nay chưa ở trạng thái trống sạch.", $"{denHomNay.Count} phòng", "Giục buồng phòng dọn / kiểm tra, hoặc đổi sang phòng khác.", "BUONG_PHONG",
                denHomNay.Select(x => $"{x.MaPhong} ({x.TinhTrang}) - phiếu {x.MaPhieuDat}"));

        // 5. Bảo trì quá hạn: phòng OOO lâu, sự cố mở lâu, thiết bị hỏng lâu
        var lichSuOoo = await _context.Phongs.AsNoTracking().Where(x => x.TinhTrang == TinhTrangPhong.OOO)
            .Select(x => new { x.MaPhong, Tu = _context.LichSuTinhTrangPhongs.Where(h => h.MaPhong == x.MaPhong && h.DenTinhTrang == TinhTrangPhong.OOO).OrderByDescending(h => h.ThoiGian).Select(h => (DateTime?)h.ThoiGian).FirstOrDefault() }).ToListAsync();
        var oooLau = lichSuOoo.Where(x => x.Tu is not null && bayGio - x.Tu.Value > TimeSpan.FromDays(7)).OrderBy(x => x.Tu).ToList();
        var suCoLau = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => (x.TrangThai == TrangThaiSuCo.MOI || x.TrangThai == TrangThaiSuCo.DA_TIEP_NHAN || x.TrangThai == TrangThaiSuCo.DANG_XU_LY) && x.ThoiGianBao < bayGio.AddDays(-SoNgaySuCoQuaHan))
            .OrderBy(x => x.ThoiGianBao).Select(x => new { x.MaSuCo, x.MaPhong, x.TieuDe, x.ThoiGianBao }).ToListAsync();
        var csvcHong = await _context.CoSoVatChats.AsNoTracking()
            .Where(x => (x.TinhTrang == "HONG_CHO_SUA" || x.TinhTrang == "HONG_DANG_SUA") && x.TinhTrangTuLuc != null && x.TinhTrangTuLuc < bayGio.AddDays(-FacilityService.HongQuaHanNgay))
            .OrderBy(x => x.TinhTrangTuLuc).Select(x => new { x.MaSo, x.Ten, x.MaPhong, x.TinhTrangTuLuc }).ToListAsync();
        var tongBaoTri = oooLau.Count + suCoLau.Count + csvcHong.Count;
        if (tongBaoTri > 0)
            Them("BAO_TRI_QUA_HAN", "CANH_BAO", "Bảo trì quá hạn",
                $"{oooLau.Count} phòng ngừng phục vụ quá 7 ngày · {suCoLau.Count} sự cố mở quá {SoNgaySuCoQuaHan} ngày · {csvcHong.Count} thiết bị hỏng quá {FacilityService.HongQuaHanNgay} ngày chưa xử lý xong.",
                $"{tongBaoTri} mục", "Đẩy nhanh sửa chữa hoặc điều chỉnh kế hoạch bảo trì.", "PHONG",
                oooLau.Select(x => $"Phòng {x.MaPhong} OOO {(int)(bayGio - x.Tu!.Value).TotalDays} ngày")
                    .Concat(suCoLau.Select(x => $"SC{x.MaSuCo:0000} phòng {x.MaPhong}: {x.TieuDe} ({(int)(bayGio - x.ThoiGianBao).TotalDays} ngày)"))
                    .Concat(csvcHong.Select(x => $"{x.Ten} ({x.MaPhong ?? "kho"}) hỏng {(int)(bayGio - x.TinhTrangTuLuc!.Value).TotalDays} ngày")));

        // 6. Hủy / không đến cao
        if (kpi.SoDatPhongMoi > 0 && kpi.TyLeHuyPhanTram > NguongTyLeHuy)
            Them("TY_LE_HUY_CAO", "CANH_BAO", "Tỷ lệ hủy đặt phòng cao", $"{kpi.SoHuy} phiếu hủy trong kỳ.", $"{Pct(kpi.TyLeHuyPhanTram)} (ngưỡng {Pct(NguongTyLeHuy)})",
                "Xem lý do hủy ở tab Đặt phòng; xem lại chính sách cọc.", "DAT_PHONG");
        if (kpi.SoDatPhongMoi > 0 && kpi.TyLeKhongDenPhanTram > NguongTyLeKhongDen)
            Them("TY_LE_KHONG_DEN_CAO", "CANH_BAO", "Tỷ lệ khách không đến cao", $"{kpi.SoKhongDen} phiếu khách không đến trong kỳ.", $"{Pct(kpi.TyLeKhongDenPhanTram)} (ngưỡng {Pct(NguongTyLeKhongDen)})",
                "Xác nhận lại với khách trước ngày đến; thu cọc đủ.", "DAT_PHONG");
        var quaNgayDon = await _context.PhieuDatPhongs.CountAsync(x => (x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN) && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null);
        if (quaNgayDon > 0)
            Them("PHIEU_QUA_NGAY_DON", "CANH_BAO", "Phiếu đặt quá ngày đón chưa xử lý", $"{quaNgayDon} phiếu quá ngày đón dự kiến mà khách chưa nhận phòng.", $"{quaNgayDon} phiếu", "Chuyển 'không đến' hoặc liên hệ khách để giải phóng phòng.", "DAT_PHONG");

        // 7. Công nợ quá hạn
        var no = await LayHoaDonConNoAsync();
        var quaHan = no.Where(x => x.NgayLap < bayGio.AddDays(-SoNgayCongNoQuaHan)).OrderBy(x => x.NgayLap).ToList();
        if (quaHan.Count > 0)
            Them("CONG_NO_QUA_HAN", "CANH_BAO", "Công nợ quá hạn", $"{quaHan.Count} hóa đơn còn nợ quá {SoNgayCongNoQuaHan} ngày.", $"{Vnd(quaHan.Sum(x => x.ConNo))} / {quaHan.Count} hóa đơn",
                "Nhắc khách thanh toán hoặc xử lý theo quy định.", "HOA_DON", quaHan.Select(x => $"{x.MaHD} - {x.HoTen ?? "?"} - nợ {Vnd(x.ConNo)} ({(int)(bayGio - x.NgayLap).TotalDays} ngày)"));

        // 8. Doanh thu phòng chưa ghi nhận trên hóa đơn
        var rc = await TinhDoiChieuAsync(from, to);
        if (rc.DoanhThuPhongTheoDem > 0 && PhanTram(rc.TienDemChuaCoHoaDon, rc.DoanhThuPhongTheoDem) >= NguongChuaGhiNhan)
            Them("DOANH_THU_CHUA_GHI_NHAN", "THONG_TIN", "Phần lớn doanh thu phòng chưa vào hóa đơn",
                $"{rc.SoDemChuaCoHoaDon} đêm ({Vnd(rc.TienDemChuaCoHoaDon)}) thuộc lượt ở chưa có hóa đơn đã thu.", $"{Pct(PhanTram(rc.TienDemChuaCoHoaDon, rc.DoanhThuPhongTheoDem))} doanh thu phòng",
                "Kiểm tra khách đang ở / hóa đơn chưa thanh toán.", "HOA_DON");

        // 9. Các cảnh báo vận hành khác (cùng nguồn với chuông cảnh báo): cái nào chưa được bao phủ ở trên
        var daCo = new HashSet<string>(items.Select(x => x.Ma)) { "BAO_TRI_QUA_LAU", "SU_CO_MUC_CAO", "CONG_NO_QUA_HAN", "PHONG_GIU_KHONG_SAN_SANG", "KHONG_DEN_CHO_XU_LY" };
        foreach (var a in (await _alerts.GetAlertsAsync()).CanhBaoDangCo.Where(a => !daCo.Contains(a.Ma)))
            Them(a.Ma, a.MucDo, a.TieuDe, a.MoTa, $"{a.SoLuong} mục", "Mở màn hình xử lý tương ứng.", a.ManHinh, a.ChiTiet);

        var thuTu = new Dictionary<string, int> { ["NGHIEM_TRONG"] = 0, ["CANH_BAO"] = 1, ["THONG_TIN"] = 2 };
        return new ReportInsightsDto { CapNhatLuc = bayGio, TuNgay = from, DenNgay = to, Items = items.OrderBy(x => thuTu.GetValueOrDefault(x.MucDo, 3)).ToList() };
    }

    // =====================================================================================================
    // Truy vết: các dòng dữ liệu tạo nên một chỉ số
    // =====================================================================================================

    private const int SoDongToiDa = 500;

    public async Task<ReportDrillDto> GetDrilldownAsync(string loai, DateOnly from, DateOnly to, string? khoa)
    {
        KiemTraKhoang(from, to);
        var l = (loai ?? "").Trim().ToUpperInvariant();
        var k = (khoa ?? "").Trim();
        var kKey = k.Contains(':') ? k[..k.IndexOf(':')] : "";
        var kVal = k.Contains(':') ? k[(k.IndexOf(':') + 1)..] : "";
        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var dto = new ReportDrillDto { Loai = l, Khoa = k, TuNgay = from, DenNgay = to };
        List<ReportDrillRowDto> dong;

        // Khóa lọc sai định dạng / không thuộc loại truy vết này phải báo lỗi, không được bỏ qua âm thầm (người xem sẽ tưởng đã lọc).
        void KiemTraKhoa(params string[] choPhep)
        {
            if (k.Length == 0) return;
            var hopLe = kKey.Length > 0 && kVal.Length > 0 && choPhep.Contains(kKey)
                && (kKey != "tang" || int.TryParse(kVal, out _))
                && (kKey != "ngay" || DateOnly.TryParse(kVal, out _));
            if (!hopLe)
                throw new ApiException(StatusCodes.Status400BadRequest, "KHOA_TRUY_VET_KHONG_HOP_LE",
                    choPhep.Length == 0 ? $"Loại truy vết {l} không dùng khóa lọc." : $"Khóa lọc phải có dạng <{string.Join("|", choPhep)}>:<giá trị hợp lệ> (VD ngay:2026-10-01, tang:2).", "khoa");
        }

        switch (l)
        {
            case "HOA_DON":
            {
                KiemTraKhoa("ngay");
                dto.Tieu = "Hóa đơn tạo nên doanh thu (lập trong kỳ, đã thu ít nhất một phần)";
                var ds = await _context.HoaDons.AsNoTracking()
                    .Where(x => x.NgayLap >= tuLuc && x.NgayLap < denLuc && x.TrangThaiThanhToan != TrangThaiThanhToan.CHUA_THANH_TOAN)
                    .Select(x => new
                    {
                        x.MaHD, x.NgayLap, x.TienPhong, x.TienDV, x.PhuThu, x.TienDaCoc, x.TongTien, x.TrangThaiThanhToan,
                        HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : null
                    }).OrderBy(x => x.NgayLap).ToListAsync();
                if (kKey == "ngay" && DateOnly.TryParse(kVal, out var ngay)) ds = ds.Where(x => DateOnly.FromDateTime(x.NgayLap) == ngay).ToList();
                dong = ds.Select(x => new ReportDrillRowDto
                {
                    Ma = x.MaHD, Ngay = x.NgayLap, DoiTuong = x.HoTen ?? "", SoTien = x.TongTien + x.TienDaCoc, TrangThai = x.TrangThaiThanhToan == TrangThaiThanhToan.DA_THANH_TOAN ? "Đã thanh toán" : "Thanh toán một phần",
                    MoTa = $"Phòng {Vnd(x.TienPhong)} · dịch vụ {Vnd(x.TienDV)} · phụ thu {Vnd(x.PhuThu)} · cọc đã trừ {Vnd(x.TienDaCoc)}"
                }).ToList();
                dto.TongKpi = k == "" ? (await LayHoaDonAsync(from, to)).Sum(x => x.DoanhThu) : null;
                break;
            }
            case "DEM_PHONG":
            {
                KiemTraKhoa("loai", "nguon", "tang", "phong", "ngay");
                dto.Tieu = "Đêm phòng đã bán (từng phòng, từng đêm, giá chốt của đêm đó)";
                var dem = await LayDemPhongAsync(from, to);
                var loaiTen = await _context.LoaiPhongs.AsNoTracking().ToDictionaryAsync(x => x.MaLoai, x => x.TenLoai);
                var tang = await _context.Phongs.AsNoTracking().ToDictionaryAsync(x => x.MaPhong, x => x.Tang);
                IEnumerable<DemPhong> q = dem;
                if (kKey == "loai") q = q.Where(x => x.MaLoai == kVal);
                else if (kKey == "nguon") q = q.Where(x => (x.Nguon?.ToString() ?? "VANG_LAI") == kVal);
                else if (kKey == "tang" && int.TryParse(kVal, out var t)) q = q.Where(x => tang.GetValueOrDefault(x.MaPhong) == t);
                else if (kKey == "phong") q = q.Where(x => x.MaPhong == kVal);
                else if (kKey == "ngay" && DateOnly.TryParse(kVal, out var ng)) q = q.Where(x => x.Ngay == ng);
                dong = q.OrderBy(x => x.Ngay).ThenBy(x => x.MaPhong, StringComparer.Ordinal).Select(x => new ReportDrillRowDto
                {
                    Ma = x.MaPhong, Ngay = x.Ngay.ToDateTime(TimeOnly.MinValue), DoiTuong = x.MaPhieuDat ?? "Khách vãng lai", SoTien = x.DonGia,
                    TrangThai = x.Nguon is null ? "Vãng lai" : x.Nguon == NguonDatPhong.WEBSITE ? "Website" : "Quầy lễ tân",
                    MoTa = $"{loaiTen.GetValueOrDefault(x.MaLoai, x.MaLoai)} · tầng {tang.GetValueOrDefault(x.MaPhong)}"
                }).ToList();
                dto.TongKpi = k == "" ? dem.Sum(x => x.DonGia) : null;
                break;
            }
            case "DAT_PHONG":
            {
                KiemTraKhoa();
                dto.Tieu = "Đặt phòng mới trong kỳ (theo ngày đặt)";
                var ds = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.NgayDat >= tuLuc && x.NgayDat < denLuc)
                    .Select(x => new { x.MaPhieuDat, x.NgayDat, x.TrangThai, x.NguonDat, x.NgayDonDuKien, x.NgayTraDuKien, HoTen = x.Khach.HoTen, Tong = x.ChiTietPhieuDats.Sum(c => (decimal?)c.ThanhTien) ?? 0 })
                    .OrderBy(x => x.NgayDat).ToListAsync();
                dong = ds.Select(x => new ReportDrillRowDto
                {
                    Ma = x.MaPhieuDat, Ngay = x.NgayDat, DoiTuong = x.HoTen, SoTien = x.Tong, TrangThai = x.TrangThai.ToString(),
                    MoTa = $"{x.NgayDonDuKien:dd/MM} → {x.NgayTraDuKien:dd/MM} · {(x.NguonDat == NguonDatPhong.WEBSITE ? "Website" : "Quầy lễ tân")}"
                }).ToList();
                dto.GhiChu = "Số tiền = tổng tiền phòng dự kiến của phiếu.";
                dto.TongKpi = null;
                dto.Khop = (await _context.PhieuDatPhongs.CountAsync(x => x.NgayDat >= tuLuc && x.NgayDat < denLuc)) == dong.Count;
                break;
            }
            case "HUY_KHONG_DEN":
            {
                KiemTraKhoa();
                dto.Tieu = "Phiếu hủy / khách không đến xảy ra trong kỳ (theo ngày hủy)";
                var ds = await _context.PhieuDatPhongs.AsNoTracking()
                    .Where(x => (x.TrangThai == TrangThaiPhieuDat.DA_HUY || x.TrangThai == TrangThaiPhieuDat.KHONG_DEN) && x.NgayHuy >= tuLuc && x.NgayHuy < denLuc)
                    .Select(x => new { x.MaPhieuDat, x.NgayHuy, x.TrangThai, x.LyDoHuy, x.HuyBoi, x.TienCoc, x.SoTienHoanCoc, x.TienCocGiuLai, HoTen = x.Khach.HoTen }).OrderBy(x => x.NgayHuy).ToListAsync();
                var tenNguoiHuy = await TenNguoiDung.HienThiAsync(_context, ds.Select(x => x.HuyBoi));
                dong = ds.Select(x => new ReportDrillRowDto
                {
                    Ma = x.MaPhieuDat, Ngay = x.NgayHuy, DoiTuong = x.HoTen, SoTien = x.TienCoc, TrangThai = x.TrangThai == TrangThaiPhieuDat.DA_HUY ? "Đã hủy" : "Không đến",
                    MoTa = $"Bởi {(x.HuyBoi is null ? "?" : tenNguoiHuy.GetValueOrDefault(x.HuyBoi, x.HuyBoi))}: {x.LyDoHuy ?? "(không ghi lý do)"} · hoàn {Vnd(x.SoTienHoanCoc)} · giữ lại {Vnd(x.TienCocGiuLai)}"
                }).ToList();
                dto.GhiChu = "Số tiền = tiền cọc của phiếu.";
                break;
            }
            case "CONG_NO":
            {
                KiemTraKhoa();
                dto.Tieu = "Hóa đơn còn nợ tại thời điểm xem";
                var no = await LayHoaDonConNoAsync();
                var bayGio = DateTime.Now;
                dong = no.OrderBy(x => x.NgayLap).Select(x => new ReportDrillRowDto
                {
                    Ma = x.MaHD, Ngay = x.NgayLap, DoiTuong = x.HoTen ?? "", SoTien = x.ConNo, TrangThai = x.NgayLap < bayGio.AddDays(-SoNgayCongNoQuaHan) ? "Quá hạn" : "Trong hạn",
                    MoTa = $"Tổng {Vnd(x.TongTien)} · đã thu {Vnd(x.DaThu)} · phiếu {x.MaPhieuDat ?? "vãng lai"}"
                }).ToList();
                dto.TongKpi = no.Sum(x => x.ConNo);
                dto.GhiChu = "Công nợ không phụ thuộc kỳ báo cáo.";
                break;
            }
            default:
                throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_TRUY_VET_KHONG_HOP_LE", "Loại truy vết chỉ nhận: HOA_DON, DEM_PHONG, DAT_PHONG, HUY_KHONG_DEN, CONG_NO.", "loai");
        }

        dto.SoDong = dong.Count;
        dto.TongSoTien = dong.Sum(x => x.SoTien);
        dto.BiCatBot = dong.Count > SoDongToiDa;
        dto.Dong = dong.Take(SoDongToiDa).ToList();
        if (dto.TongKpi is { } kpiTong) dto.Khop = kpiTong == dto.TongSoTien;
        return dto;
    }
}
