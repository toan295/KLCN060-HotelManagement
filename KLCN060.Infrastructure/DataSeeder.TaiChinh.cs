using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

public static partial class DataSeeder
{
    /// <summary>
    /// Bổ sung số liệu cho đối soát chuyển khoản và sổ cọc, IDEMPOTENT (chỉ đụng tới dòng còn thiếu) nên chạy lại ở mọi lần khởi động:
    /// (1) thông báo tiền về bất thường (lệch số tiền, sai nội dung, muộn...) được đánh dấu CAN_XU_LY để kế toán thấy trong hàng chờ;
    /// (2) phiếu đặt đã nhận cọc nhưng chưa có thời điểm/hình thức nhận cọc (dữ liệu trước khi có sổ cọc): lấy từ giao dịch chuyển khoản thành công,
    /// nếu không có thì từ nhật ký "xác nhận cọc tiền mặt", cuối cùng mới đến ngày đặt phiếu.
    /// </summary>
    private static async Task SeedSoQuyTaiChinhAsync(KLCN060DbContext context)
    {
        await context.ThongBaoNganHangs
            .Where(x => x.KetQua != KetQuaDoiSoatNganHang.KHOP && x.TrangThaiXuLy == TrangThaiXuLyDoiSoat.KHONG_CAN)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThaiXuLy, TrangThaiXuLyDoiSoat.CAN_XU_LY));

        var chuaCoNgayCoc = await context.PhieuDatPhongs
            .Where(x => x.NgayNhanCoc == null && x.TienCoc > 0
                && (x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.DA_NHAN_PHONG || x.TrangThai == TrangThaiPhieuDat.DA_TRA_PHONG
                    || x.SoTienHoanCoc > 0 || x.TienCocGiuLai > 0))
            .ToListAsync();
        if (chuaCoNgayCoc.Count > 0)
        {
            var maPhieus = chuaCoNgayCoc.Select(x => x.MaPhieuDat).ToList();
            var chuyenKhoan = (await context.GiaoDichChuyenKhoans.AsNoTracking()
                    .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.THANH_CONG && maPhieus.Contains(x.MaThamChieu))
                    .ToListAsync())
                .GroupBy(x => x.MaThamChieu).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.NgayThanhToan).First());
            var tienMat = (await context.NhatKyThaoTacs.AsNoTracking()
                    .Where(x => x.HanhDong == "XAC_NHAN_COC" && x.DoiTuongTacDong != null && maPhieus.Contains(x.DoiTuongTacDong))
                    .ToListAsync())
                .GroupBy(x => x.DoiTuongTacDong!).ToDictionary(g => g.Key, g => g.OrderBy(x => x.ThoiGian).First());

            foreach (var p in chuaCoNgayCoc)
            {
                if (chuyenKhoan.TryGetValue(p.MaPhieuDat, out var ck))
                {
                    p.NgayNhanCoc = ck.NgayThanhToan ?? ck.NgayTao;
                    p.HinhThucCoc = HinhThucThanhToan.CHUYEN_KHOAN;
                    p.NguoiNhanCoc = ck.MaTaiKhoanTao;
                }
                else if (tienMat.TryGetValue(p.MaPhieuDat, out var nk))
                {
                    p.NgayNhanCoc = nk.ThoiGian;
                    p.HinhThucCoc = HinhThucThanhToan.TIEN_MAT;
                    p.NguoiNhanCoc = nk.MaTaiKhoan;
                }
                else
                {
                    p.NgayNhanCoc = p.NgayDat;
                    p.HinhThucCoc = HinhThucThanhToan.TIEN_MAT;
                }
            }
            await context.SaveChangesAsync();
        }

    }
}

public static partial class DataSeeder
{
    /// <summary>
    /// Dữ liệu mẫu cho màn "Đối soát chuyển khoản" (thông báo bất thường đã xử lý) để kế toán xem được cả hai trạng thái của danh sách. IDEMPOTENT: nhận biết bằng mã giao dịch ngân hàng bắt đầu "MAU-" nên chạy lại không thêm trùng; không đụng dữ liệu thật.
    /// </summary>
    private static async Task SeedMauDoiSoatMauAsync(KLCN060DbContext context)
    {
        if (await context.ThongBaoNganHangs.AnyAsync(x => x.MaGiaoDichNganHang.StartsWith("MAU-"))) return;

        var bayGio = DateTime.Now;
        var soTaiKhoan = await context.ThongBaoNganHangs.Select(x => x.SoTaiKhoanNhan).FirstOrDefaultAsync() ?? "0123456789";

        ThongBaoNganHang Tb(string ma, decimal tien, string noiDung, KetQuaDoiSoatNganHang ketQua, int ngayTruoc, string? ghiChu, HuongXuLyDoiSoat huong, string ghiChuXuLy, string nguoi = "ketoan")
        {
            var luc = bayGio.AddDays(-ngayTruoc);
            return new ThongBaoNganHang
            {
                MaGiaoDichNganHang = ma, SoTaiKhoanNhan = soTaiKhoan, SoTien = tien, NoiDung = noiDung,
                ThoiGianNganHang = luc, ThoiGianNhan = luc.AddMinutes(1), KetQua = ketQua, GhiChu = ghiChu,
                TrangThaiXuLy = TrangThaiXuLyDoiSoat.DA_XU_LY, NguoiXuLy = nguoi, NgayXuLy = luc.AddHours(3),
                HuongXuLy = huong.ToString(), GhiChuXuLy = ghiChuXuLy
            };
        }

        var tbHoan1 = Tb("MAU-FT26100301", 500_000, "KLCN060 GD2026100301 CHUYEN THUA", KetQuaDoiSoatNganHang.SAI_SO_TIEN, 9, "Khách chuyển dư 200.000đ", HuongXuLyDoiSoat.DA_XU_LY_NGOAI, "Khách chuyển dư 200.000đ so với số cần thanh toán, đã chuyển khoản trả lại khách ngoài hệ thống.");
        var tbHoan2 = Tb("MAU-FT26100302", 150_000, "NGUYEN VAN B CHUYEN NHAM", KetQuaDoiSoatNganHang.KHONG_TIM_THAY_GIAO_DICH, 7, "Không có mã giao dịch trong nội dung", HuongXuLyDoiSoat.DA_XU_LY_NGOAI, "Khách báo chuyển nhầm tài khoản, đã trả lại đúng số tiền tại quầy.");
        var tbNgoai = Tb("MAU-FT26100303", 820_000, "KLCN060 GD2026100250", KetQuaDoiSoatNganHang.GIAO_DICH_KHONG_CON_HIEU_LUC, 8, "Tiền về sau khi mã QR hết hạn", HuongXuLyDoiSoat.DA_XU_LY_NGOAI, "Đã liên hệ khách và ghi nhận thu bổ sung vào hóa đơn HD20260920-0001.");
        var tbBoQua = Tb("MAU-FT26100304", 10_000, "TEST CHUYEN KHOAN", KetQuaDoiSoatNganHang.KHONG_TIM_THAY_GIAO_DICH, 6, "Giao dịch thử của ngân hàng", HuongXuLyDoiSoat.BO_QUA, "Giao dịch kiểm tra đường truyền của ngân hàng, không phải tiền khách.", "ketoan");
        context.ThongBaoNganHangs.AddRange(tbHoan1, tbHoan2, tbNgoai, tbBoQua);
        await context.SaveChangesAsync();
    }
}
