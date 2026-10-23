using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

/// <summary>Dữ liệu mẫu và nâng cấp dữ liệu cũ cho các chức năng điều hành của Quản lý (nguồn đặt phòng, hủy phiếu, lịch sử tình trạng phòng, bảng giá).</summary>
public static partial class DataSeeder
{
    /// <param name="laLanDauCoDanhMucMoi">true ở lần khởi động đầu tiên sau khi CSDL có danh mục quyền điều hành (CSDL mới hoặc vừa nâng cấp).</param>
    /// <param name="laCsdlMoi">true khi CSDL vừa được tạo mới (chưa có phòng nào trước lần seed này).</param>
    private static async Task SeedDieuHanhAsync(KLCN060DbContext context, bool laLanDauCoDanhMucMoi, bool laCsdlMoi)
    {
        if (laLanDauCoDanhMucMoi)
        {
            await GanNguonDatVaThongTinHuyAsync(context);
            await SeedLichSuTinhTrangPhongAsync(context, laCsdlMoi);
        }
        await SeedQuyTacGiaMauAsync(context);
        await SeedGoiDichVuMauAsync(context);
    }

    /// <summary>
    /// Phiếu đặt lập trước khi có cột NguonDat: khách có tài khoản đăng nhập được coi là đặt qua Website (ước lượng, chỉ cho dữ liệu cũ/mẫu).
    /// Phiếu đã hủy / không đến được bổ sung thời điểm hủy và số cọc hoàn theo đúng chính sách hiện hành (hoàn khi hủy từ 30 ngày trước ngày đón).
    /// </summary>
    private static async Task GanNguonDatVaThongTinHuyAsync(KLCN060DbContext context)
    {
        var phieus = await context.PhieuDatPhongs.Include(x => x.Khach).ThenInclude(k => k.TaiKhoan).ToListAsync();
        foreach (var p in phieus)
        {
            if (p.Khach.TaiKhoan is not null) p.NguonDat = NguonDatPhong.WEBSITE;

            if (p.TrangThai is TrangThaiPhieuDat.DA_HUY or TrangThaiPhieuDat.KHONG_DEN && p.NgayHuy is null)
            {
                var ngayDon = p.NgayDonDuKien.ToDateTime(TimeOnly.MinValue);
                if (p.TrangThai == TrangThaiPhieuDat.KHONG_DEN)
                {
                    p.NgayHuy = ngayDon.AddDays(1);
                    p.LyDoHuy = "Khách không đến nhận phòng";
                }
                else
                {
                    p.NgayHuy = new[] { p.NgayDat.AddDays(2), DateTime.Now }.Min();
                    p.LyDoHuy = "Khách hủy đặt phòng";
                    var conLai = p.NgayDonDuKien.DayNumber - DateOnly.FromDateTime(p.NgayHuy.Value).DayNumber;
                    if (conLai >= 30) p.SoTienHoanCoc = p.TienCoc;
                    else p.TienCocGiuLai = p.TienCoc;
                }
                if (p.TrangThai == TrangThaiPhieuDat.KHONG_DEN) p.TienCocGiuLai = p.TienCoc;
                p.HuyBoi = "letan";
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedLichSuTinhTrangPhongAsync(KLCN060DbContext context, bool laCsdlMoi)
    {
        if (await context.LichSuTinhTrangPhongs.AnyAsync())
        {
            // Dữ liệu mẫu cũ khóa bảo trì chưa ghi người khóa: bổ sung để cột "Người khóa" không để trống.
            await context.LichSuTinhTrangPhongs
                .Where(x => x.DenTinhTrang == TinhTrangPhong.OOO && x.MaTaiKhoan == null && x.LyDo == "Máy lạnh hỏng, chờ thợ sửa")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaTaiKhoan, "quanly"));
            return;
        }

        var bayGio = DateTime.Now;
        var phongs = await context.Phongs.Where(x => x.TinhTrang != TinhTrangPhong.VC).ToListAsync();
        foreach (var p in phongs)
        {
            // CSDL mới: dựng lịch sử mẫu (P103 bảo trì lâu ngày, P203 chưa dọn từ sáng...) để bảng điều hành có cảnh báo thực tế.
            // CSDL cũ nâng cấp: không biết phòng ở trạng thái này từ bao giờ nên tính từ lúc nâng cấp để không báo động giả.
            var (thoiGian, lyDo) = !laCsdlMoi ? (bayGio, "Bắt đầu theo dõi lịch sử tình trạng phòng") : p.TinhTrang switch
            {
                TinhTrangPhong.OOO => (bayGio.AddDays(-12), "Máy lạnh hỏng, chờ thợ sửa"),
                TinhTrangPhong.VD when p.MaPhong == "P203" => (bayGio.AddHours(-6), "Khách vừa trả phòng"),
                TinhTrangPhong.VD => (bayGio.AddHours(-1), "Khách vừa trả phòng"),
                _ => (bayGio.AddDays(-1), "Nhận phòng")
            };
            context.LichSuTinhTrangPhongs.Add(new LichSuTinhTrangPhong
            {
                MaPhong = p.MaPhong,
                TuTinhTrang = p.TinhTrang is TinhTrangPhong.OC ? TinhTrangPhong.VC : p.TinhTrang is TinhTrangPhong.VD ? TinhTrangPhong.OC : TinhTrangPhong.VC,
                DenTinhTrang = p.TinhTrang,
                ThoiGian = thoiGian,
                MaTaiKhoan = p.TinhTrang == TinhTrangPhong.OOO && laCsdlMoi ? "quanly" : null,
                LyDo = lyDo
            });
        }
        await context.SaveChangesAsync();
    }

    /// <summary>Hai quy tắc giá mẫu ở trạng thái TẮT (không làm đổi giá của dữ liệu/kiểm thử hiện có): Quản lý bật lên ở màn hình Bảng giá khi cần.</summary>
    private static async Task SeedQuyTacGiaMauAsync(KLCN060DbContext context)
    {
        // CSDL đã seed trước đó: chỉ đổi tên mẫu cũ "Giá đoàn / doanh nghiệp -10%" thành "Giá đoàn -10%" (khách hàng đoàn gọi thống nhất là "Đoàn").
        var cu = await context.QuyTacGias.Where(x => x.TenQuyTac == "Giá đoàn / doanh nghiệp -10%").ToListAsync();
        if (cu.Count > 0) { foreach (var q in cu) q.TenQuyTac = "Giá đoàn -10%"; await context.SaveChangesAsync(); }
        if (await context.QuyTacGias.AnyAsync()) return;

        var bayGio = DateTime.Now;
        context.QuyTacGias.AddRange(
            new QuyTacGia
            {
                TenQuyTac = "Giá cuối tuần (đêm thứ 6, thứ 7) +20%",
                LoaiQuyTac = LoaiQuyTacGia.CUOI_TUAN,
                KieuDieuChinh = KieuDieuChinhGia.PHAN_TRAM,
                GiaTri = 20m,
                CacThuApDung = "5,6",
                UuTien = 10,
                HoatDong = false,
                GhiChu = "Quy tắc mẫu - bật lên để áp dụng.",
                NgayTao = bayGio
            },
            new QuyTacGia
            {
                TenQuyTac = "Giá đoàn -10%",
                LoaiQuyTac = LoaiQuyTacGia.DOANH_NGHIEP,
                KieuDieuChinh = KieuDieuChinhGia.PHAN_TRAM,
                GiaTri = -10m,
                LoaiDatPhongApDung = LoaiDatPhong.DOAN,
                UuTien = 5,
                HoatDong = false,
                GhiChu = "Quy tắc mẫu - áp dụng cho phiếu đặt loại ĐOÀN.",
                NgayTao = bayGio
            });
        await context.SaveChangesAsync();
    }

    /// <summary>Một gói dịch vụ mẫu (giặt ủi + nước ngọt) giá thấp hơn mua lẻ, để màn hình Gói dịch vụ không trống ở CSDL mới.</summary>
    private static async Task SeedGoiDichVuMauAsync(KLCN060DbContext context)
    {
        if (await context.GoiDichVus.AnyAsync()) return;
        var coDv01 = await context.DichVus.AnyAsync(x => x.MaDV == "DV01");
        var coDv04 = await context.DichVus.AnyAsync(x => x.MaDV == "DV04");
        if (!coDv01 || !coDv04) return;

        var goi = new GoiDichVu
        {
            MaGoi = "GD01",
            TenGoi = "Gói tiện ích: giặt ủi 2kg + 2 nước ngọt",
            MoTa = "Gói mẫu - giá thấp hơn mua lẻ (100.000đ).",
            GiaGoi = 90000m,
            HoatDong = true,
            NgayTao = DateTime.Now
        };
        goi.ChiTietGois.Add(new ChiTietGoiDichVu { MaGoi = "GD01", MaDV = "DV01", SoLuong = 2 });
        goi.ChiTietGois.Add(new ChiTietGoiDichVu { MaGoi = "GD01", MaDV = "DV04", SoLuong = 2 });
        context.GoiDichVus.Add(goi);
        await context.SaveChangesAsync();
    }
}
