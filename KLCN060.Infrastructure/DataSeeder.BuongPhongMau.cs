using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

// Dữ liệu mẫu bổ sung cho nghiệp vụ buồng phòng. Idempotent theo từng nhóm (chỉ chèn khi còn thiếu), chạy được cả trên CSDL cũ
// mà không ghi đè dữ liệu người dùng đã nhập.
public static partial class DataSeeder
{
    private static readonly string[] TenBuongPhongThem = { "buongphong2", "buongphong3", "buongphong4" };

    private static async Task SeedBuongPhongMauAsync(KLCN060DbContext context)
    {
        await SeedNhanVienBuongPhongThemAsync(context);
        await SeedVatDungTheoLoaiPhongAsync(context);
        await SeedPhieuDonChoPhongDaDonAsync(context);
        await SeedSuCoMauAsync(context);
        await SeedMinibarMauAsync(context);
        await SeedDoBoQuenMauAsync(context);
    }

    /// <summary>Thêm 3 nhân viên buồng phòng (đăng nhập buongphong2/3/4, mật khẩu mặc định) để quản lý có người để giao việc.</summary>
    private static async Task SeedNhanVienBuongPhongThemAsync(KLCN060DbContext context)
    {
        var vaiTro = await context.VaiTros.FirstOrDefaultAsync(x => x.TenVaiTro == "BUONG_PHONG");
        if (vaiTro is null) return;

        var mau = new[]
        {
            (Ten: "buongphong2", Ma: "NV06", HoTen: "Lê Thị Hoa", Sdt: "0900000006"),
            (Ten: "buongphong3", Ma: "NV07", HoTen: "Trần Văn Nam", Sdt: "0900000007"),
            (Ten: "buongphong4", Ma: "NV08", HoTen: "Nguyễn Thị Lan", Sdt: "0900000008")
        };
        var daCoTk = (await context.TaiKhoans.Where(x => TenBuongPhongThem.Contains(x.TenDN)).Select(x => x.TenDN).ToListAsync()).ToHashSet();
        var maNvDaCo = (await context.NhanViens.Select(x => x.MaNV).ToListAsync()).ToHashSet();
        var sdtDaCo = (await context.NhanViens.Select(x => x.SoDT).ToListAsync()).ToHashSet();
        var themGi = false;
        foreach (var m in mau)
        {
            if (daCoTk.Contains(m.Ten) || maNvDaCo.Contains(m.Ma) || sdtDaCo.Contains(m.Sdt)) continue;
            context.NhanViens.Add(new NhanVien { MaNV = m.Ma, HoTen = m.HoTen, SoDT = m.Sdt, ChucVu = "Buồng phòng", MaVaiTro = vaiTro.MaVaiTro });
            context.TaiKhoans.Add(new TaiKhoan
            {
                TenDN = m.Ten, MatKhau = BCrypt.Net.BCrypt.HashPassword(MatKhauMacDinh), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = m.Ma, TrangThai = TrangThaiTaiKhoan.HOAT_DONG
            });
            themGi = true;
        }
        if (themGi) await context.SaveChangesAsync();
    }

    /// <summary>
    /// Áo choàng tắm chỉ có ở phòng Deluxe (ví dụ phạm vi theo loại phòng), và định mức theo giường / người cho CSDL mới.
    /// </summary>
    private static async Task SeedVatDungTheoLoaiPhongAsync(KLCN060DbContext context)
    {
        if (!await context.VatDungs.AnyAsync() || await context.VatDungs.AnyAsync(x => x.TenVatDung == "Áo choàng tắm")) return;
        var coDeluxe = await context.LoaiPhongs.AnyAsync(x => x.MaLoai == "LP03");
        var ma = (await context.VatDungs.Select(x => x.MaVatDung).ToListAsync())
            .Select(x => int.TryParse(x.Replace("VT", ""), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var maMoi = $"VT{ma:00}";
        var vatDung = new VatDung
        {
            MaVatDung = maMoi, TenVatDung = "Áo choàng tắm", Nhom = NhomVatDung.AMENITIES, DonViTinh = "cái", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI
        };
        if (coDeluxe) vatDung.LoaiPhongApDungs.Add(new VatDungLoaiPhong { MaVatDung = maMoi, MaLoai = "LP03" });
        context.VatDungs.Add(vatDung);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Phòng đã dọn (VC) hoặc đã kiểm tra (VI) thì phải biết ai dọn: ghi phiếu dọn đã xong cho những phòng chưa có (dữ liệu mẫu cũ đặt thẳng tình trạng phòng).
    /// </summary>
    private static async Task SeedPhieuDonChoPhongDaDonAsync(KLCN060DbContext context)
    {
        var nhanVien = await context.TaiKhoans.AsNoTracking()
            .Where(x => x.TenDN == "buongphong" || TenBuongPhongThem.Contains(x.TenDN)).Select(x => x.TenDN).OrderBy(x => x).ToListAsync();
        if (nhanVien.Count == 0) return;

        var phongs = await context.Phongs.AsNoTracking().Where(x => x.TinhTrang == TinhTrangPhong.VC || x.TinhTrang == TinhTrangPhong.VI).ToListAsync();
        var daCo = (await context.PhieuDonPhongs.Where(x => x.TrangThai == TrangThaiPhieuDon.DA_XONG).Select(x => x.MaPhong).Distinct().ToListAsync()).ToHashSet();
        var i = 0;
        foreach (var p in phongs.Where(x => !daCo.Contains(x.MaPhong)).OrderBy(x => x.MaPhong, StringComparer.Ordinal))
        {
            var luc = await context.LichSuTinhTrangPhongs.AsNoTracking()
                .Where(x => x.MaPhong == p.MaPhong && x.DenTinhTrang == p.TinhTrang).OrderByDescending(x => x.ThoiGian).Select(x => (DateTime?)x.ThoiGian).FirstOrDefaultAsync()
                ?? DateTime.Now.AddHours(-1);
            context.PhieuDonPhongs.Add(new PhieuDonPhong
            {
                MaPhong = p.MaPhong, NguoiDon = nhanVien[i++ % nhanVien.Count], TinhTrangBanDau = TinhTrangPhong.VD,
                ThoiGianBatDau = luc.AddMinutes(-30), ThoiGianXong = luc, TrangThai = TrangThaiPhieuDon.DA_XONG
            });
        }
        if (i > 0) await context.SaveChangesAsync();
    }

    private static async Task SeedSuCoMauAsync(KLCN060DbContext context)
    {
        if (await context.SuCoPhongs.CountAsync() >= 4) return;
        var phongs = await context.Phongs.AsNoTracking().OrderBy(x => x.MaPhong).Select(x => x.MaPhong).Take(8).ToListAsync();
        if (phongs.Count < 6) return;
        var bayGio = DateTime.Now;
        string P(int i) => phongs[i % phongs.Count];

        context.SuCoPhongs.AddRange(
            new SuCoPhong { MaPhong = P(0), Loai = LoaiSuCo.DIEU_HOA, TieuDe = "Điều hòa không lạnh", MoTa = "Bật 16 độ nhưng chỉ ra gió thường, có tiếng kêu lạ ở dàn lạnh.", MucDo = MucDoSuCo.CAO,
                TrangThai = TrangThaiSuCo.MOI, NguoiBao = "buongphong", ThoiGianBao = bayGio.AddHours(-3) },
            new SuCoPhong { MaPhong = P(1), Loai = LoaiSuCo.NUOC, TieuDe = "Vòi sen nhà tắm rỉ nước", MoTa = "Vòi sen nhỏ giọt liên tục khi đã khóa, nước chảy xuống sàn.", MucDo = MucDoSuCo.TRUNG_BINH,
                TrangThai = TrangThaiSuCo.DA_TIEP_NHAN, NguoiBao = "buongphong2", ThoiGianBao = bayGio.AddHours(-20), NguoiXuLy = "quanly", ThoiGianTiepNhan = bayGio.AddHours(-18) },
            new SuCoPhong { MaPhong = P(2), Loai = LoaiSuCo.DIEN_CHIEU_SANG, TieuDe = "Bóng đèn đầu giường cháy", MoTa = "Đèn ngủ bên trái giường không sáng, đã thử đổi ổ cắm.", MucDo = MucDoSuCo.THAP,
                TrangThai = TrangThaiSuCo.DANG_XU_LY, NguoiBao = "letan", ThoiGianBao = bayGio.AddDays(-1).AddHours(-2), NguoiXuLy = "quanly", ThoiGianTiepNhan = bayGio.AddDays(-1) },
            new SuCoPhong { MaPhong = P(3), Loai = LoaiSuCo.TV_THIET_BI_DIEN_TU, TieuDe = "TV mất tín hiệu", MoTa = "Màn hình báo không có tín hiệu, các kênh đều trắng.", MucDo = MucDoSuCo.TRUNG_BINH,
                TrangThai = TrangThaiSuCo.DA_XU_LY, NguoiBao = "buongphong3", ThoiGianBao = bayGio.AddDays(-3), NguoiXuLy = "quanly", ThoiGianTiepNhan = bayGio.AddDays(-3).AddHours(1),
                ThoiGianXong = bayGio.AddDays(-2).AddHours(-20), GhiChuXuLy = "Cắm lại dây anten và dò kênh lại, TV đã có tín hiệu.", NguoiDong = "quanly" },
            new SuCoPhong { MaPhong = P(4), Loai = LoaiSuCo.NOI_THAT, TieuDe = "Tủ quần áo hỏng bản lề", MoTa = "Cánh tủ bên phải bị lệch, khó đóng.", MucDo = MucDoSuCo.THAP,
                TrangThai = TrangThaiSuCo.DA_HUY, NguoiBao = "buongphong4", ThoiGianBao = bayGio.AddDays(-2), NguoiXuLy = "quanly", ThoiGianXong = bayGio.AddDays(-2).AddHours(2),
                GhiChuXuLy = "Báo nhầm phòng, cánh tủ đã được khách đóng lại bình thường.", NguoiDong = "quanly" },
            new SuCoPhong { MaPhong = P(5), Loai = LoaiSuCo.NUOC, TieuDe = "Rò nước từ trần nhà tắm", MoTa = "Nước nhỏ từ trần xuống bồn rửa mặt, trần có vết ố loang.", MucDo = MucDoSuCo.KHAN_CAP,
                TrangThai = TrangThaiSuCo.MOI, NguoiBao = "buongphong2", ThoiGianBao = bayGio.AddMinutes(-40) });
        await context.SaveChangesAsync();
    }

    /// <summary>Hai phiếu kiểm minibar ở phòng đang có khách: khoản chờ tính phí cho lễ tân, và một khoản đã được lễ tân bỏ qua có lý do.</summary>
    private static async Task SeedMinibarMauAsync(KLCN060DbContext context)
    {
        const string DauMau = "[Dữ liệu mẫu]";
        if (await context.PhieuKiemPhongs.AnyAsync(x => x.GhiChu != null && x.GhiChu.StartsWith(DauMau))) return;
        var coca = await context.VatDungs.Include(x => x.DichVu).FirstOrDefaultAsync(x => x.MaVatDung == "VT06" && x.MaDV != null);
        var bia = await context.VatDungs.Include(x => x.DichVu).FirstOrDefaultAsync(x => x.MaVatDung == "VT07" && x.MaDV != null);
        if (coca?.DichVu is null || bia?.DichVu is null) return;

        var dangO = await context.ChiTietPhieuNhans.AsNoTracking().Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .OrderBy(x => x.MaPhong).Select(x => new { x.MaPhong, x.MaPhieuNhan }).Take(3).ToListAsync();
        if (dangO.Count == 0) return;
        var bayGio = DateTime.Now;

        ChiTietKiemPhong Dong(VatDung v, int truoc, int sau, TrangThaiTinhPhi tt, string? lyDoBoQua = null) => new()
        {
            MaVatDung = v.MaVatDung, SoLuongTruoc = truoc, SoLuongSau = sau, LyDo = LyDoHaoHut.TIEU_THU,
            SoLuongTinhPhi = truoc - sau, ThanhTien = v.DichVu!.GiaDV * (truoc - sau), TrangThaiTinhPhi = tt,
            NguoiXuLy = tt == TrangThaiTinhPhi.BO_QUA ? "letan" : null, ThoiGianXuLy = tt == TrangThaiTinhPhi.BO_QUA ? bayGio.AddHours(-1) : null, LyDoBoQua = lyDoBoQua
        };

        var p1 = new PhieuKiemPhong { MaPhong = dangO[0].MaPhong, MaPhieuNhan = dangO[0].MaPhieuNhan, NguoiKiem = "buongphong", ThoiGian = bayGio.AddHours(-3), GhiChu = DauMau + " Kiểm phòng buổi sáng" };
        p1.ChiTiets.Add(Dong(coca, 2, 0, TrangThaiTinhPhi.CHO_TINH_PHI));
        p1.ChiTiets.Add(Dong(bia, 2, 1, TrangThaiTinhPhi.CHO_TINH_PHI));
        context.PhieuKiemPhongs.Add(p1);

        if (dangO.Count > 1)
        {
            var p2 = new PhieuKiemPhong { MaPhong = dangO[1].MaPhong, MaPhieuNhan = dangO[1].MaPhieuNhan, NguoiKiem = "buongphong2", ThoiGian = bayGio.AddHours(-2), GhiChu = DauMau };
            p2.ChiTiets.Add(Dong(coca, 2, 1, TrangThaiTinhPhi.CHO_TINH_PHI));
            context.PhieuKiemPhongs.Add(p2);
        }
        if (dangO.Count > 2)
        {
            var p3 = new PhieuKiemPhong { MaPhong = dangO[2].MaPhong, MaPhieuNhan = dangO[2].MaPhieuNhan, NguoiKiem = "buongphong3", ThoiGian = bayGio.AddHours(-5), GhiChu = DauMau };
            p3.ChiTiets.Add(Dong(bia, 2, 1, TrangThaiTinhPhi.BO_QUA, "Khách VIP, khách sạn tặng 1 lon bia chào đón"));
            context.PhieuKiemPhongs.Add(p3);
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedDoBoQuenMauAsync(KLCN060DbContext context)
    {
        if (await context.DoBoQuens.CountAsync() >= 3) return;
        var phongs = await context.Phongs.AsNoTracking().OrderBy(x => x.MaPhong).Select(x => x.MaPhong).Take(6).ToListAsync();
        if (phongs.Count < 4) return;
        var bayGio = DateTime.Now;

        context.DoBoQuens.AddRange(
            new DoBoQuen { MaPhong = phongs[0], ThoiGianPhatHien = bayGio.AddDays(-2), Loai = LoaiDoBoQuen.GIAY_TO, MoTa = "Căn cước công dân của nam, trong ngăn kéo tủ đầu giường",
                NguoiNhat = "Phạm Thị Phòng", NguoiGhiNhan = "buongphong", ViTriLuuGiu = "Tủ A - ngăn 2", TrangThai = TrangThaiDoBoQuen.DANG_LUU_GIU },
            new DoBoQuen { MaPhong = phongs[1], ThoiGianPhatHien = bayGio.AddHours(-5), Loai = LoaiDoBoQuen.DIEN_TU, MoTa = "Củ sạc điện thoại màu trắng kèm dây Type-C, cắm ở ổ điện cạnh bàn",
                NguoiNhat = "Lê Thị Hoa", NguoiGhiNhan = "buongphong2", TrangThai = TrangThaiDoBoQuen.DA_TIM_THAY },
            new DoBoQuen { KhuVuc = "Sảnh đón tiếp", ThoiGianPhatHien = bayGio.AddDays(-4), Loai = LoaiDoBoQuen.VI_TIEN, MoTa = "Ví da màu nâu, bên trong có thẻ ngân hàng và ít tiền mặt",
                NguoiNhat = "Trần Thị Tân", NguoiGhiNhan = "letan", ViTriLuuGiu = "Két lễ tân", TrangThai = TrangThaiDoBoQuen.DANG_LUU_GIU },
            new DoBoQuen { MaPhong = phongs[2], ThoiGianPhatHien = bayGio.AddDays(-6), Loai = LoaiDoBoQuen.QUAN_AO, MoTa = "Áo khoác gió màu xanh navy, cỡ L",
                NguoiNhat = "Trần Văn Nam", NguoiGhiNhan = "buongphong3", ViTriLuuGiu = "Tủ B - ngăn 1", TrangThai = TrangThaiDoBoQuen.DA_TRA_KHACH,
                ThoiGianTra = bayGio.AddDays(-5), NguoiNhanLai = "Ngô Thanh Tùng", GiayToNguoiNhan = "CCCD 079093003344", NguoiTra = "letan", GhiChuTra = "Khách quay lại nhận trực tiếp" },
            new DoBoQuen { MaPhong = phongs[3], ThoiGianPhatHien = bayGio.AddDays(-40), Loai = LoaiDoBoQuen.TRANG_SUC, MoTa = "Dây chuyền bạc có mặt hình trái tim",
                NguoiNhat = "Nguyễn Thị Lan", NguoiGhiNhan = "buongphong4", ViTriLuuGiu = "Tủ A - ngăn 3", TrangThai = TrangThaiDoBoQuen.DA_THANH_LY,
                ThoiGianThanhLy = bayGio.AddDays(-5), NguoiThanhLy = "quanly", LyDoThanhLy = "Quá 30 ngày không ai nhận, đã liên hệ khách theo thông tin đặt phòng nhưng không phản hồi" });
        await context.SaveChangesAsync();
    }
}
