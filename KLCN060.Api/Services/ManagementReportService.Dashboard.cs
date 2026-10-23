using KLCN060.Api.DTOs.Reports;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class ManagementReportService
{
    private static readonly TrangThaiPhieuDat[] PhieuDangGiuPhong =
        { TrangThaiPhieuDat.CHO_XAC_NHAN, TrangThaiPhieuDat.DA_XAC_NHAN, TrangThaiPhieuDat.DA_NHAN_PHONG };

    /// <summary>Bổ sung cho bảng điều hành: số hôm qua (ngữ cảnh), việc phải làm hôm nay, dự báo 7 ngày, và đối chiếu nhất quán giữa các số.</summary>
    private async Task DienDashboardMoRongAsync(DashboardDto dto, DateOnly homNay, DateOnly dauThang, int tongPhong)
    {
        // ----- Hôm qua -----
        var homQua = homNay.AddDays(-1);
        var hdQua = await LayHoaDonAsync(homQua, homQua);
        var demQua = await LayDemPhongAsync(homQua, homQua);
        var kpiQua = TinhKpi(demQua, KhaDung(tongPhong, _ngayTaoPhong, homQua, 1));
        dto.HomQua = new DashboardHomQuaDto { DoanhThu = hdQua.Sum(x => x.DoanhThu), CongSuatPhanTram = kpiQua.CongSuatPhanTram, Adr = kpiQua.Adr, SoDemPhongBan = demQua.Count };

        // ----- Việc hôm nay -----
        var den = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN && x.NgayDonDuKien == homNay && x.PhieuNhanPhong == null)
            .Select(x => new
            {
                x.MaPhieuDat, HoTen = x.Khach.HoTen,
                Phongs = x.ChiTietPhieuDats.Select(c => new { c.MaPhong, c.Phong.TinhTrang }).ToList()
            }).ToListAsync();
        var sanSang = new[] { TinhTrangPhong.VC, TinhTrangPhong.VI };
        dto.ViecHomNay.TongKhachDenChuaNhan = den.Count;
        dto.ViecHomNay.KhachDenChuaNhan = den.OrderBy(x => x.Phongs.All(p => sanSang.Contains(p.TinhTrang)) ? 1 : 0).ThenBy(x => x.MaPhieuDat).Take(8).Select(x =>
        {
            var chua = x.Phongs.Where(p => !sanSang.Contains(p.TinhTrang)).ToList();
            return new DashboardViecDto
            {
                Ma = x.MaPhieuDat, TieuDe = $"{x.MaPhieuDat} · {x.HoTen}",
                ChiTiet = $"Phòng {string.Join(", ", x.Phongs.Select(p => p.MaPhong))}" + (chua.Count == 0 ? " - đã sẵn sàng" : $" - CHƯA SẴN SÀNG: {string.Join(", ", chua.Select(p => $"{p.MaPhong} ({p.TinhTrang})"))}"),
                CanChuY = chua.Count > 0, ManHinh = chua.Count > 0 ? "BUONG_PHONG" : "DAT_PHONG"
            };
        }).ToList();

        // Ngày trả dự kiến lấy theo từng lượt ở (đã tính gia hạn, có cả khách vãng lai); lượt cũ chưa có thì dùng ngày trả của phiếu đặt.
        var tra = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O
                && (x.NgayTraDuKien ?? (x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.NgayTraDuKien : (DateOnly?)null)) == homNay)
            .Select(x => new
            {
                x.MaPhong, x.PhieuNhanPhong.MaPhieuNhan,
                HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen
                    : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : "Khách vãng lai"
            }).ToListAsync();
        dto.ViecHomNay.TongKhachTraChuaTra = tra.Count;
        dto.ViecHomNay.KhachTraChuaTra = tra.OrderBy(x => x.MaPhong).Take(8).Select(x => new DashboardViecDto
        {
            Ma = x.MaPhieuNhan, TieuDe = $"Phòng {x.MaPhong} · {x.HoTen}", ChiTiet = $"Phiếu nhận {x.MaPhieuNhan} - dự kiến trả hôm nay", CanChuY = false, ManHinh = "LUU_TRU"
        }).ToList();

        // ----- Dự báo 7 ngày -----
        // Phiếu đặt chưa nhận phòng (đang giữ chỗ) + khách ĐANG Ở (kể cả vãng lai, theo ngày trả dự kiến của từng lượt; phòng đã trả sớm thì bỏ).
        var giu = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(x => x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
            .Select(x => new { x.MaPhong, Don = x.PhieuDatPhong.NgayDonDuKien, Tra = x.PhieuDatPhong.NgayTraDuKien, Ma = x.MaPhieuDat }).ToListAsync();
        var o = await _context.ChiTietPhieuNhans.AsNoTracking().Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => new
            {
                x.MaPhong, Don = x.NgayNhan, Ma = x.MaPhieuNhan,
                Tra = x.NgayTraDuKien ?? (x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.NgayTraDuKien : (DateOnly?)null)
            }).ToListAsync();
        var luuTru = o.Select(x => new { x.MaPhong, x.Don, Tra = x.Tra ?? homNay.AddDays(1), x.Ma }).ToList();
        for (var i = 0; i < 7; i++)
        {
            var ngay = homNay.AddDays(i);
            var phongDat = giu.Where(x => x.Don <= ngay && ngay < x.Tra).Select(x => x.MaPhong);
            // Khách quá hạn chưa trả vẫn đang chiếm phòng hôm nay.
            var phongO = luuTru.Where(x => x.Don <= ngay && (ngay < x.Tra || (x.Tra <= homNay && ngay == homNay))).Select(x => x.MaPhong);
            var phong = phongDat.Concat(phongO).Distinct().Count();
            dto.DuBao.Add(new DashboardDuBaoDto
            {
                Ngay = ngay, PhongDaGiu = phong,
                KhachDen = giu.Where(x => x.Don == ngay).Select(x => x.Ma).Distinct().Count(),
                KhachTra = giu.Where(x => x.Tra == ngay).Select(x => x.Ma).Concat(luuTru.Where(x => x.Tra == ngay).Select(x => x.Ma)).Distinct().Count(),
                CongSuatDuKienPhanTram = PhanTram(phong, tongPhong)
            });
        }

        // ----- Đối chiếu nhất quán -----
        var d = dto.DoiChieu;
        void Them(string ten, decimal a, decimal b, string ghiChu = "", decimal dungSai = 0, string? ta = null, string? tb = null)
            => d.Phep.Add(new ReportCheckDto { Ten = ten, VeTrai = ta ?? Vnd(a), VePhai = tb ?? Vnd(b), Khop = Math.Abs(a - b) <= dungSai, GhiChu = ghiChu });
        string So(decimal x) => x.ToString("0.##");

        var p = dto.Phong;
        var tongTinhTrang = p.TheoTinhTrang.Values.Sum();
        Them("Tổng số phòng = tổng các tình trạng (VC+VD+VI+OC+OD+OOO)", p.TongSoPhong, tongTinhTrang, "", 0, p.TongSoPhong.ToString(), tongTinhTrang.ToString());
        Them("Tổng số phòng = tổng theo tầng", p.TongSoPhong, dto.TheoTang.Sum(x => x.TongSoPhong), "", 0, p.TongSoPhong.ToString(), dto.TheoTang.Sum(x => x.TongSoPhong).ToString());
        var vd = p.TheoTinhTrang.GetValueOrDefault("VD");
        Them("Có khách + Trống + Chưa dọn (VD) + Bảo trì = tổng phòng", p.DangCoKhach + p.Trong + vd + p.BaoTri, p.TongSoPhong, "Có khách = OC+OD; Trống = VC+VI",
            0, (p.DangCoKhach + p.Trong + vd + p.BaoTri).ToString(), p.TongSoPhong.ToString());
        Them("Công suất tức thời = phòng có khách ÷ tổng phòng", p.CongSuatPhanTram, PhanTram(p.DangCoKhach, p.TongSoPhong), "", 0.01m, So(p.CongSuatPhanTram) + "%", So(PhanTram(p.DangCoKhach, p.TongSoPhong)) + "%");
        var dt = dto.DoanhThu;
        Them("RevPAR hôm nay ≈ ADR × công suất hôm nay", dt.RevParHomNay, Math.Round(dt.AdrHomNay * dt.CongSuatHomNayPhanTram / 100m, 0), "Lệch tối đa vài đồng do làm tròn ADR và công suất", Math.Max(1m, dt.AdrHomNay * 0.001m));
        Them("RevPAR tháng này ≈ ADR × công suất tháng", dt.RevParThang, Math.Round(dt.AdrThang * dt.CongSuatThangPhanTram / 100m, 0), "Lệch tối đa vài đồng do làm tròn ADR và công suất", Math.Max(1m, dt.AdrThang * 0.001m));
        var congNoChiTiet = await LayHoaDonConNoAsync();
        Them("Công nợ: tổng trên bảng = tổng theo từng hóa đơn", dto.CongNo.TongConNo, congNoChiTiet.Sum(x => x.ConNo));
        Them("Công nợ quá hạn ≤ tổng công nợ", Math.Min(dto.CongNo.TongQuaHan, dto.CongNo.TongConNo), dto.CongNo.TongQuaHan, "Phần quá hạn không thể lớn hơn tổng nợ");

        // Doanh thu / công suất / ADR / RevPAR của THÁNG NÀY đối chiếu với các báo cáo (13 phép của màn Báo cáo)
        var dc = await GetReconciliationAsync(dauThang, homNay);
        d.Phep.AddRange(dc.KiemTra.Select(k => new ReportCheckDto { Ten = "Tháng này · " + k.Ten, VeTrai = k.VeTrai, VePhai = k.VePhai, Khop = k.Khop, GhiChu = k.GhiChu }));
        d.TongSoPhep = d.Phep.Count;
        d.SoKhop = d.Phep.Count(x => x.Khop);
    }
}
