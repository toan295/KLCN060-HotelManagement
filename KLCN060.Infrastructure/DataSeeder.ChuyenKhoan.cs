using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

// Dữ liệu mẫu cho thanh toán chuyển khoản mô phỏng: để màn "Đối soát chuyển khoản" có sẵn dữ liệu (cả khoản khớp lẫn
// khoản hết hạn) ngay sau khi tạo mới CSDL, không cần tự thao tác từng tình huống.
public static partial class DataSeeder
{
    // Trùng giá trị mặc định của Payment:Bank:AccountNumber trong appsettings.json của API.
    private const string TaiKhoanNganHangMau = "0123456789";

    private static async Task SeedChuyenKhoanMauAsync(KLCN060DbContext context)
    {
        // Đã có giao dịch từ trước (CSDL cũ) thì không tạo thêm các yêu cầu hết hạn mẫu, nhưng vẫn bổ sung bản ghi đối soát
        // cho những lần thanh toán chuyển khoản chưa có (vd. các lần thanh toán thẻ cũ đã được migration đổi sang chuyển khoản).
        var daCoGiaoDich = await context.GiaoDichChuyenKhoans.AnyAsync();

        var bayGio = DateTime.Now;
        var maDaCo = (await context.GiaoDichChuyenKhoans.Select(x => x.MaGiaoDich).ToListAsync()).ToHashSet();
        var soThuTuTheoNgay = new Dictionary<string, int>();
        string SinhMa(DateTime ngay)
        {
            var khoa = ngay.ToString("yyyyMMdd");
            string ma;
            do
            {
                soThuTuTheoNgay[khoa] = soThuTuTheoNgay.GetValueOrDefault(khoa) + 1;
                ma = $"GD{khoa}-{soThuTuTheoNgay[khoa]:0000}";
            } while (maDaCo.Contains(ma));   // không trùng mã của giao dịch đã có (CSDL cũ)
            maDaCo.Add(ma);
            return ma;
        }

        // Tài khoản đứng tên thu ngân theo MaNV.
        var taiKhoanTheoNV = await context.TaiKhoans.Where(t => t.MaNV != null)
            .ToDictionaryAsync(t => t.MaNV!, t => t.TenDN);

        // 1) Mọi lần thanh toán CHUYEN_KHOAN đã có trong dữ liệu mẫu -> giao dịch THANH_CONG + thông báo ngân hàng KHOP.
        var daDoiSoat = await context.ThongBaoNganHangs.Select(x => x.MaGiaoDichNganHang).ToListAsync();
        var daThanhToan = (await context.ChiTietThanhToans
            .Where(x => x.HinhThucThanhToan == HinhThucThanhToan.CHUYEN_KHOAN && x.MaGiaoDich != null)
            .OrderBy(x => x.ThoiGianThanhToan)
            .ToListAsync())
            .Where(x => !daDoiSoat.Contains(x.MaGiaoDich!))
            .ToList();

        foreach (var tt in daThanhToan)
        {
            if (!taiKhoanTheoNV.TryGetValue(tt.MaTaiKhoanThuNgan, out var tenDN)) continue;

            var ma = SinhMa(tt.ThoiGianThanhToan);
            var gd = new GiaoDichChuyenKhoan
            {
                MaGiaoDich = ma,
                LoaiThamChieu = LoaiThamChieuThanhToan.HOA_DON,
                MaThamChieu = tt.MaHD,
                SoTien = tt.SoTien,
                NoiDungChuyenKhoan = $"KLCN060 {ma.Replace("-", string.Empty)}",
                TrangThai = TrangThaiGiaoDichChuyenKhoan.THANH_CONG,
                NgayTao = tt.ThoiGianThanhToan.AddMinutes(-4),
                HanThanhToan = tt.ThoiGianThanhToan.AddMinutes(11),
                NgayThanhToan = tt.ThoiGianThanhToan,
                MaGiaoDichNganHang = tt.MaGiaoDich,
                MaTaiKhoanTao = tenDN,
                MaNVThuNgan = tt.MaTaiKhoanThuNgan
            };
            context.GiaoDichChuyenKhoans.Add(gd);
            context.ThongBaoNganHangs.Add(new ThongBaoNganHang
            {
                MaGiaoDichNganHang = tt.MaGiaoDich!,
                SoTaiKhoanNhan = TaiKhoanNganHangMau,
                SoTien = tt.SoTien,
                NoiDung = gd.NoiDungChuyenKhoan,
                ThoiGianNganHang = tt.ThoiGianThanhToan,
                ThoiGianNhan = tt.ThoiGianThanhToan,
                KetQua = KetQuaDoiSoatNganHang.KHOP,
                MaGiaoDich = ma,
                GhiChu = "Đã đối soát khớp và ghi nhận thanh toán."
            });
        }

        // 2) Vài yêu cầu chuyển khoản HẾT HẠN (khách mở mã QR nhưng không chuyển tiền), gắn với phiếu đặt đang chờ cọc nếu có.
        // Ngân hàng từ chối mọi khoản chuyển sai thông tin nên không có "tiền về sai" nào cần đối soát thủ công.
        if (daCoGiaoDich)
        {
            await context.SaveChangesAsync();
            return;
        }

        var nguoiTao = taiKhoanTheoNV.Values.Contains("letan") ? "letan" : taiKhoanTheoNV.Values.FirstOrDefault();
        var phieuChoCoc = await context.PhieuDatPhongs
            .Where(x => x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN && x.TienCoc > 0)
            .OrderBy(x => x.MaPhieuDat)
            .Take(2)
            .ToListAsync();

        if (nguoiTao is not null)
        {
            var thuTu = 0;
            foreach (var p in phieuChoCoc)
            {
                thuTu++;
                var tao = bayGio.AddDays(-thuTu).AddHours(-1);
                var ma = SinhMa(tao);
                context.GiaoDichChuyenKhoans.Add(new GiaoDichChuyenKhoan
                {
                    MaGiaoDich = ma,
                    LoaiThamChieu = LoaiThamChieuThanhToan.DAT_COC,
                    MaThamChieu = p.MaPhieuDat,
                    SoTien = p.TienCoc,
                    NoiDungChuyenKhoan = $"KLCN060 {ma.Replace("-", string.Empty)}",
                    TrangThai = TrangThaiGiaoDichChuyenKhoan.HET_HAN,
                    NgayTao = tao,
                    HanThanhToan = tao.AddMinutes(15),
                    MaTaiKhoanTao = nguoiTao
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
