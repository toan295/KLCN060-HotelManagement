using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

/// <summary>
/// Seed dữ liệu mẫu — chạy tại mỗi lần khởi động ứng dụng, IDEMPOTENT (mỗi bảng chỉ chèn khi đang rỗng),
/// nên nếu xóa database rồi chạy lại (migrate lại từ đầu), toàn bộ dữ liệu mẫu dưới đây tự động có lại,
/// không cần thao tác tay. Các mốc ngày của đặt phòng/lưu trú/hóa đơn được tính TƯƠNG ĐỐI theo thời điểm
/// seed chạy (hôm nay ± N ngày) để dữ liệu luôn hợp lý bất kể seed vào ngày nào.
/// </summary>
public static partial class DataSeeder
{
    private const string MatKhauMacDinh = "Klcn060@2026";

    public static async Task SeedAsync(KLCN060DbContext context)
    {
        var laCsdlMoi = !await context.Phongs.AnyAsync();
        await SeedVaiTroAsync(context);
        var quyenMoi = await SeedQuyenAsync(context);
        await SeedVaiTroQuyenAsync(context, quyenMoi);
        await GoQuyenDoiSoatKhoiQuanLyAsync(context);
        await GoQuyenPhongKhoiLeTanAsync(context);
        await GoQuyenDaBoAsync(context);
        await SeedNhanVienAsync(context);
        await SeedKhachAsync(context);
        await SeedKhachBoSungAsync(context);
        await SeedTaiKhoanAsync(context);
        await SeedLoaiPhongAsync(context);
        await SeedPhongAsync(context);
        await SeedCoSoVatChatAsync(context);
        await SeedDichVuAsync(context);
        await SeedChiTietDichVuAsync(context);
        await SeedVatDungAsync(context, quyenMoi.Contains(Permissions.GhiNhanBuongPhong));
        await SeedKhuyenMaiAsync(context);
        await SeedCauHoiThuongGapAsync(context);
        // Nhóm dữ liệu vận hành (đặt phòng/nhận phòng/dịch vụ đã dùng/hóa đơn) phụ thuộc lẫn nhau theo
        // đúng thứ tự nghiệp vụ thật (đặt -> nhận -> dùng dịch vụ -> trả -> xuất hóa đơn) nên gom seed
        // trong 1 phương thức duy nhất, kiểm tra rỗng theo PhieuDatPhong (bảng gốc của cả chuỗi).
        await SeedVongDoiLuuTruMauAsync(context);
        await SeedLuuTruMoRongAsync(context);
        await SeedLuuTruMoRongAsync(context, boSung: true);
        await BoSungNgayTraDuKienAsync(context);
        await SeedChuyenKhoanMauAsync(context);
        await SeedDieuHanhAsync(context, quyenMoi.Contains(Permissions.XemNhatKy), laCsdlMoi);
        await SeedSoQuyTaiChinhAsync(context);
        await SeedMauDoiSoatMauAsync(context);
        await SeedBuongPhongMauAsync(context);
    }

    private static async Task SeedVaiTroAsync(KLCN060DbContext context)
    {
        if (await context.VaiTros.AnyAsync()) return;

        context.VaiTros.AddRange(
            new VaiTro { TenVaiTro = "QUAN_LY", MoTa = "Quản lý khách sạn" },
            new VaiTro { TenVaiTro = "LE_TAN", MoTa = "Lễ tân" },
            new VaiTro { TenVaiTro = "BUONG_PHONG", MoTa = "Buồng phòng" },
            new VaiTro { TenVaiTro = "KE_TOAN", MoTa = "Kế toán" },
            new VaiTro { TenVaiTro = "KHACH_HANG", MoTa = "Khách hàng" }
        );
        await context.SaveChangesAsync();
    }

    // Danh mục quyền (KLCN060.Domain.Permissions) mở rộng theo thời gian: seed BỔ SUNG những quyền còn thiếu thay vì chỉ chèn khi bảng rỗng,
    // để CSDL cũ (chỉ có 5 quyền đầu tiên) được nâng lên danh mục đầy đủ mà không mất các chỉnh sửa phân quyền đã làm.
    /// <summary>Trả về tên các quyền vừa được thêm ở lần seed này (rỗng nếu CSDL đã đủ danh mục).</summary>
    private static async Task<HashSet<string>> SeedQuyenAsync(KLCN060DbContext context)
    {
        var daCo = (await context.Quyens.Select(x => x.TenQuyen).ToListAsync()).ToHashSet();
        var thieu = Permissions.DanhMuc.Where(x => !daCo.Contains(x.TenQuyen)).ToList();
        if (thieu.Count == 0) return new HashSet<string>();

        context.Quyens.AddRange(thieu.Select(x => new Quyen { TenQuyen = x.TenQuyen, NhomChucNang = x.NhomChucNang }));
        await context.SaveChangesAsync();
        return thieu.Select(x => x.TenQuyen).ToHashSet();
    }

    // Chỉ gán quyền MẶC ĐỊNH cho những quyền vừa được thêm (CSDL mới: tất cả; CSDL cũ: phần bổ sung) - không ghi đè phân quyền
    // mà Quản lý đã chỉnh ở D15.
    private static async Task SeedVaiTroQuyenAsync(KLCN060DbContext context, HashSet<string> moi)
    {
        if (moi.Count == 0) return;

        var vaiTros = await context.VaiTros.ToDictionaryAsync(x => x.TenVaiTro, x => x.MaVaiTro);
        var quyens = await context.Quyens.ToDictionaryAsync(x => x.TenQuyen, x => x.MaQuyen);
        var conVaiTroQuyenCu = await context.VaiTroQuyens.AnyAsync();

        // Nâng cấp từ danh mục 5 quyền cũ: trước đây Quản lý được gán "cả 5 quyền" nên có luôn THAO_TAC_LE_TAN, trong khi thao tác tại
        // quầy (nhận/trả phòng, thu cọc, tạo khách...) là việc của Lễ tân. Chỉ làm một lần, ngay lúc bổ sung danh mục mới.
        if (conVaiTroQuyenCu && moi.Contains(Permissions.XemPhong) && vaiTros.TryGetValue("QUAN_LY", out var maQuanLy))
        {
            var thaoTacLeTan = quyens[Permissions.ThaoTacLeTan];
            var cu = await context.VaiTroQuyens.Where(x => x.MaVaiTro == maQuanLy && x.MaQuyen == thaoTacLeTan).ToListAsync();
            context.VaiTroQuyens.RemoveRange(cu);
        }

        // Nâng cấp lên danh mục điều hành: Quản lý cần tự chuyển phòng vào/ra bảo trì (khóa/mở phòng) nên được bổ sung DOI_TINH_TRANG_PHONG (quyền này
        // đã có từ trước nên không nằm trong danh sách "quyền mới" ở dưới). Chỉ làm một lần, ngay lúc bổ sung danh mục điều hành.
        // (Nếu DOI_TINH_TRANG_PHONG cũng là quyền mới - CSDL rất cũ - thì vòng lặp mặc định bên dưới đã gán, không gán hai lần.)
        if (conVaiTroQuyenCu && moi.Contains(Permissions.XemNhatKy) && !moi.Contains(Permissions.DoiTinhTrangPhong) && vaiTros.TryGetValue("QUAN_LY", out var maQuanLy2))
        {
            var doiTinhTrang = quyens[Permissions.DoiTinhTrangPhong];
            if (!await context.VaiTroQuyens.AnyAsync(x => x.MaVaiTro == maQuanLy2 && x.MaQuyen == doiTinhTrang))
                context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = maQuanLy2, MaQuyen = doiTinhTrang });
        }

        foreach (var (tenVaiTro, cacQuyen) in Permissions.MacDinhTheoVaiTro)
        {
            if (!vaiTros.TryGetValue(tenVaiTro, out var maVaiTro)) continue;
            foreach (var tenQuyen in cacQuyen.Where(moi.Contains))
                context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = maVaiTro, MaQuyen = quyens[tenQuyen] });
        }
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Đối soát chuyển khoản (tiền ngân hàng báo về, mã QR) là việc của Kế toán: Quản lý không còn quyền XEM_DOI_SOAT lẫn XU_LY_DOI_SOAT
    /// (mặc định mới đã bỏ). CSDL cũ từng gán các quyền này cho Quản lý nên gỡ ở đây (idempotent).
    /// </summary>
    private static Task GoQuyenDoiSoatKhoiQuanLyAsync(KLCN060DbContext context)
        => GoQuyenKhoiVaiTroAsync(context, "QUAN_LY", Permissions.XemDoiSoat, Permissions.XuLyDoiSoat);

    /// <summary>
    /// Lễ tân chỉ xem sơ đồ phòng; đổi tình trạng phòng và khóa / mở phòng bảo trì là việc của Buồng phòng và Quản lý (mặc định mới đã bỏ).
    /// CSDL cũ từng gán DOI_TINH_TRANG_PHONG, KHOA_MO_PHONG cho Lễ tân nên gỡ ở đây (idempotent).
    /// </summary>
    private static Task GoQuyenPhongKhoiLeTanAsync(KLCN060DbContext context)
        => GoQuyenKhoiVaiTroAsync(context, "LE_TAN", Permissions.DoiTinhTrangPhong, Permissions.KhoaMoPhong);

    private static async Task GoQuyenKhoiVaiTroAsync(KLCN060DbContext context, string tenVaiTro, params string[] tenQuyens)
    {
        var maVaiTro = await context.VaiTros.Where(x => x.TenVaiTro == tenVaiTro).Select(x => (int?)x.MaVaiTro).FirstOrDefaultAsync();
        if (maVaiTro is null) return;
        var maQuyens = await context.Quyens.Where(x => tenQuyens.Contains(x.TenQuyen)).Select(x => x.MaQuyen).ToListAsync();
        if (maQuyens.Count == 0) return;
        await context.VaiTroQuyens.Where(x => x.MaVaiTro == maVaiTro && maQuyens.Contains(x.MaQuyen)).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Quyền của các chức năng đã bỏ không còn trong danh mục: gỡ khỏi mọi vai trò rồi xóa quyền (idempotent).
    /// DUYET_TAI_CHINH: luồng Phê duyệt. QUAN_LY_CHI_PHI: màn Chi phí vận hành.
    /// </summary>
    private static async Task GoQuyenDaBoAsync(KLCN060DbContext context)
    {
        foreach (var tenQuyen in new[] { "DUYET_TAI_CHINH", "QUAN_LY_CHI_PHI" })
        {
            var maQuyen = await context.Quyens.Where(x => x.TenQuyen == tenQuyen).Select(x => (int?)x.MaQuyen).FirstOrDefaultAsync();
            if (maQuyen is null) continue;
            await context.VaiTroQuyens.Where(x => x.MaQuyen == maQuyen).ExecuteDeleteAsync();
            await context.Quyens.Where(x => x.MaQuyen == maQuyen).ExecuteDeleteAsync();
        }
    }

    private static async Task SeedNhanVienAsync(KLCN060DbContext context)
    {
        if (await context.NhanViens.AnyAsync()) return;

        var vaiTros = await context.VaiTros.ToDictionaryAsync(x => x.TenVaiTro, x => x.MaVaiTro);

        context.NhanViens.AddRange(
            new NhanVien { MaNV = "NV01", HoTen = "Nguyễn Văn Quản", SoDT = "0900000001", ChucVu = "Quản lý", MaVaiTro = vaiTros["QUAN_LY"] },
            new NhanVien { MaNV = "NV02", HoTen = "Trần Thị Tân", SoDT = "0900000002", ChucVu = "Lễ tân", MaVaiTro = vaiTros["LE_TAN"] },
            new NhanVien { MaNV = "NV03", HoTen = "Lê Văn Toán", SoDT = "0900000003", ChucVu = "Kế toán", MaVaiTro = vaiTros["KE_TOAN"] },
            new NhanVien { MaNV = "NV04", HoTen = "Phạm Thị Phòng", SoDT = "0900000004", ChucVu = "Buồng phòng", MaVaiTro = vaiTros["BUONG_PHONG"] },
            new NhanVien { MaNV = "NV05", HoTen = "Đặng Thị Thu", SoDT = "0900000005", ChucVu = "Lễ tân", MaVaiTro = vaiTros["LE_TAN"] }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedKhachAsync(KLCN060DbContext context)
    {
        if (await context.Khachs.AnyAsync()) return;

        context.Khachs.AddRange(
            new Khach { MaKhach = "KH01", HoTen = "Đỗ Thị Hương", SoDT = "0905123456", CCCD = "079092000123", Email = "huong.do@gmail.com", DiaChi = "23 Nguyễn Thị Minh Khai, Quận 3, TP.HCM" },
            new Khach { MaKhach = "KH02", HoTen = "Nguyễn Văn An", SoDT = "0912345678", CCCD = "079095001234", Email = "an.nguyen@gmail.com", DiaChi = "12 Nguyễn Huệ, Quận 1, TP.HCM" },
            new Khach { MaKhach = "KH03", HoTen = "Trần Thị Bích Ngọc", SoDT = "0987654321", CCCD = "079094002468", Email = "ngoc.tran@gmail.com", DiaChi = "77 Điện Biên Phủ, Quận Bình Thạnh, TP.HCM" },
            new Khach { MaKhach = "KH04", HoTen = "Lê Hoàng Nam", SoDT = "0933112233", CCCD = "079096005678", DiaChi = "45 Trần Phú, Phường 3, TP. Đà Lạt" },
            new Khach { MaKhach = "KH05", HoTen = "Phạm Thị Cẩm Tú", SoDT = "0977889900", CCCD = "079091003579", Email = "tu.pham@yahoo.com", DiaChi = "102 Lý Thường Kiệt, TP. Biên Hòa" },
            new Khach { MaKhach = "KH06", HoTen = "Võ Minh Khang", SoDT = "0966554433", CCCD = "079090009999", Email = "khang.vo@outlook.com", DiaChi = "8 Lê Lợi, Quận Hải Châu, TP. Đà Nẵng" },
            new Khach { MaKhach = "KH07", HoTen = "Hoàng Thị Mai", SoDT = "0955223344", CCCD = "079089007531" },
            new Khach { MaKhach = "KH08", HoTen = "Bùi Quốc Huy", SoDT = "0944112233", CCCD = "079088004321", Email = "huy.bui@gmail.com", DiaChi = "56 Hùng Vương, TP. Đà Lạt" },
            new Khach { MaKhach = "KH09", HoTen = "Đinh Thị Lan", SoDT = "0918223344", CCCD = "079097007788", Email = "lan.dinh@gmail.com", DiaChi = "9 Phan Đình Phùng, TP. Đà Lạt" },
            new Khach { MaKhach = "KH10", HoTen = "Trịnh Văn Long (Công ty Thông Xanh)", SoDT = "02838220011", CCCD = "079080001122", Email = "dulich.thongxanh@gmail.com", DiaChi = "31 Nguyễn Đình Chiểu, Quận 3, TP.HCM" },
            new Khach { MaKhach = "KH11", HoTen = "Ngô Thanh Tùng", SoDT = "0909556677", CCCD = "079093003344", Email = "tung.ngo@gmail.com", DiaChi = "18 Pasteur, Quận 1, TP.HCM" },
            new Khach { MaKhach = "KH12", HoTen = "Lý Mỹ Duyên", SoDT = "0938778899", CCCD = "079099005566", Email = "duyen.ly@gmail.com", DiaChi = "64 Ngô Quyền, TP. Nha Trang" },
            new Khach { MaKhach = "KH13", HoTen = "Dương Văn Sơn", SoDT = "0972001122" },
            new Khach { MaKhach = "KH14", HoTen = "Mai Phương Thảo", SoDT = "0989334455", Email = "thao.mai@gmail.com" },
            new Khach { MaKhach = "KH15", HoTen = "Tô Gia Bảo", SoDT = "0921667788" }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedTaiKhoanAsync(KLCN060DbContext context)
    {
        if (await context.TaiKhoans.AnyAsync()) return;

        string Hash() => BCrypt.Net.BCrypt.HashPassword(MatKhauMacDinh);

        context.TaiKhoans.AddRange(
            new TaiKhoan { TenDN = "quanly", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = "NV01", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "letan", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = "NV02", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "ketoan", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = "NV03", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "buongphong", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = "NV04", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "letan2", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN, MaNV = "NV05", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "0905123456", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG, MaKhach = "KH01", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "0912345678", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG, MaKhach = "KH02", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "0987654321", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG, MaKhach = "KH03", TrangThai = TrangThaiTaiKhoan.HOAT_DONG },
            new TaiKhoan { TenDN = "0933112233", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG, MaKhach = "KH04", TrangThai = TrangThaiTaiKhoan.HOAT_DONG }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedLoaiPhongAsync(KLCN060DbContext context)
    {
        if (await context.LoaiPhongs.AnyAsync()) return;

        context.LoaiPhongs.AddRange(
            new LoaiPhong { MaLoai = "LP01", TenLoai = "Standard", SoNguoiTieuChuan = 2, DonGia = 450000m, PhuThu = 200000m, DienTich = 22m, LoaiGiuong = "1 giường đôi", TienNghi = "Wifi, Điều hòa, TV, Tủ lạnh mini", MoTa = "Phòng tiêu chuẩn gọn gàng cho 2 khách." },
            new LoaiPhong { MaLoai = "LP02", TenLoai = "Superior", SoNguoiTieuChuan = 2, DonGia = 550000m, PhuThu = 200000m, DienTich = 26m, LoaiGiuong = "1 giường đôi", TienNghi = "Wifi, Điều hòa, TV, Tủ lạnh mini, Ban công", MoTa = "Phòng cao cấp hơn, rộng rãi, có ban công." },
            new LoaiPhong { MaLoai = "LP03", TenLoai = "Deluxe", SoNguoiTieuChuan = 2, DonGia = 750000m, PhuThu = 200000m, DienTich = 32m, LoaiGiuong = "1 giường king", TienNghi = "Wifi, Điều hòa, TV, Tủ lạnh mini, Ban công, Bồn tắm", MoTa = "Phòng hạng sang với giường king và bồn tắm." },
            new LoaiPhong { MaLoai = "LP04", TenLoai = "Triple", SoNguoiTieuChuan = 3, DonGia = 650000m, PhuThu = 200000m, DienTich = 30m, LoaiGiuong = "1 giường đôi + 1 giường đơn", TienNghi = "Wifi, Điều hòa, TV, Tủ lạnh mini", MoTa = "Phòng ba người phù hợp gia đình nhỏ." }
        );
        await context.SaveChangesAsync();
    }

    /// <summary>12 phòng, 3 phòng/loại — đủ đa dạng trạng thái (VC/VD/VI/OC/OOO) để D2 Dashboard có dữ liệu
    /// sống động ngay từ đầu. P201/P203/P401 được cố ý khớp với các lượt lưu trú mẫu ở
    /// <see cref="SeedVongDoiLuuTruMauAsync"/> (P201 đang có khách, P203 vừa trả chưa dọn).</summary>
    private static async Task SeedPhongAsync(KLCN060DbContext context)
    {
        if (await context.Phongs.AnyAsync()) return;

        context.Phongs.AddRange(
            new Phong { MaPhong = "P101", TenPhong = "101", Tang = 1, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP01" },
            new Phong { MaPhong = "P102", TenPhong = "102", Tang = 1, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP01" },
            new Phong { MaPhong = "P103", TenPhong = "103", Tang = 1, TinhTrang = TinhTrangPhong.OOO, MaLoai = "LP01" },
            new Phong { MaPhong = "P201", TenPhong = "201", Tang = 2, TinhTrang = TinhTrangPhong.OC, MaLoai = "LP02" },
            new Phong { MaPhong = "P202", TenPhong = "202", Tang = 2, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP02" },
            new Phong { MaPhong = "P203", TenPhong = "203", Tang = 2, TinhTrang = TinhTrangPhong.VD, MaLoai = "LP02" },
            new Phong { MaPhong = "P301", TenPhong = "301", Tang = 3, TinhTrang = TinhTrangPhong.VD, MaLoai = "LP03" },
            new Phong { MaPhong = "P302", TenPhong = "302", Tang = 3, TinhTrang = TinhTrangPhong.OC, MaLoai = "LP03" },
            new Phong { MaPhong = "P303", TenPhong = "303", Tang = 3, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP03" },
            new Phong { MaPhong = "P401", TenPhong = "401", Tang = 4, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP04" },
            new Phong { MaPhong = "P402", TenPhong = "402", Tang = 4, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP04" },
            new Phong { MaPhong = "P403", TenPhong = "403", Tang = 4, TinhTrang = TinhTrangPhong.OC, MaLoai = "LP04" }
        );
        await context.SaveChangesAsync();
    }

    /// <summary>Mỗi phòng có bộ tiện nghi cơ bản (tivi, máy lạnh, bình đun; tủ lạnh mini từ Superior trở lên), vài món
    /// đang hỏng/vừa thay mới để D14 có đủ cả 3 tình trạng, cộng thiết bị dùng chung (không gắn phòng).</summary>
    private static async Task SeedCoSoVatChatAsync(KLCN060DbContext context)
    {
        if (await context.CoSoVatChats.AnyAsync()) return;

        var phongs = await context.Phongs.OrderBy(x => x.MaPhong).ToListAsync();
        var ds = new List<CoSoVatChat>();
        var stt = 1;
        string Ma() => $"CSVC{stt++:000}";

        foreach (var p in phongs)
        {
            var caoCap = p.MaLoai != "LP01";
            ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = caoCap ? "Tivi Smart 50 inch" : "Tivi Smart 43 inch", SoLuong = 1, TinhTrang = "TOT", MaPhong = p.MaPhong });
            ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Máy lạnh Daikin 1.5HP", SoLuong = 1, TinhTrang = "TOT", MaPhong = p.MaPhong });
            ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Bình đun nước siêu tốc", SoLuong = 1, TinhTrang = "TOT", MaPhong = p.MaPhong });
            if (caoCap)
                ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Tủ lạnh mini", SoLuong = 1, TinhTrang = "TOT", MaPhong = p.MaPhong });
        }

        void DoiTinhTrang(string maPhong, string ten, string tinhTrang)
        {
            var x = ds.First(d => d.MaPhong == maPhong && d.Ten.StartsWith(ten));
            x.TinhTrang = tinhTrang;
        }
        DoiTinhTrang("P103", "Máy lạnh", "HONG_DANG_SUA");
        DoiTinhTrang("P301", "Bình đun", "HONG_DANG_SUA");
        DoiTinhTrang("P401", "Bình đun", "DA_THAY_MOI");
        DoiTinhTrang("P203", "Tivi", "DA_THAY_MOI");

        ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Máy sấy tóc", SoLuong = 12, TinhTrang = "TOT", MaPhong = null });
        ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Bàn ủi", SoLuong = 3, TinhTrang = "TOT", MaPhong = null });
        ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Xe đẩy dọn phòng", SoLuong = 2, TinhTrang = "TOT", MaPhong = null });
        ds.Add(new CoSoVatChat { MaSo = Ma(), Ten = "Máy hút bụi công nghiệp", SoLuong = 1, TinhTrang = "HONG_DANG_SUA", MaPhong = null });

        context.CoSoVatChats.AddRange(ds);
        await context.SaveChangesAsync();
    }

    private static async Task SeedDichVuAsync(KLCN060DbContext context)
    {
        if (await context.DichVus.AnyAsync()) return;

        context.DichVus.AddRange(
            new DichVu { MaDV = "DV01", TenDV = "Giặt ủi", GiaDV = 35000m, DonViTinh = "kg", LoaiDoanhThu = LoaiDoanhThuDichVu.GIAT_LA },
            new DichVu { MaDV = "DV02", TenDV = "Thuê xe máy ngày thường", GiaDV = 130000m, DonViTinh = "xe/ngày" },
            new DichVu { MaDV = "DV03", TenDV = "Thuê xe máy ngày lễ", GiaDV = 180000m, DonViTinh = "xe/ngày" },
            new DichVu { MaDV = "DV04", TenDV = "Nước ngọt", GiaDV = 15000m, DonViTinh = "lon", LoaiDoanhThu = LoaiDoanhThuDichVu.MINIBAR },
            new DichVu { MaDV = "DV05", TenDV = "Bia", GiaDV = 15000m, DonViTinh = "lon", LoaiDoanhThu = LoaiDoanhThuDichVu.MINIBAR },
            new DichVu { MaDV = "DV06", TenDV = "Ăn sáng buffet", GiaDV = 90000m, DonViTinh = "suất" },
            new DichVu { MaDV = "DV07", TenDV = "Đưa đón sân bay Liên Khương", GiaDV = 350000m, DonViTinh = "lượt" }
        );
        await context.SaveChangesAsync();
    }

    /// <summary>3 khuyến mãi: 2 còn hiệu lực (1 phần trăm, 1 số tiền cố định) + 1 đã hết hạn (để test
    /// đúng lỗi KHUYEN_MAI_HET_HAN khi áp dụng).</summary>
    private static async Task SeedKhuyenMaiAsync(KLCN060DbContext context)
    {
        if (await context.KhuyenMais.AnyAsync()) return;

        var homNay = DateOnly.FromDateTime(DateTime.Now);

        context.KhuyenMais.AddRange(
            new KhuyenMai
            {
                MaKM = "KM01",
                TenKM = "Giảm 10% đặt sớm",
                LoaiKM = LoaiKhuyenMai.PHAN_TRAM,
                PhanTramKM = 10m,
                GiaTri = 10m,
                NgayBatDau = homNay.AddDays(-10),
                NgayKetThuc = homNay.AddDays(60),
                DieuKien = "Áp dụng cho mọi loại phòng, đặt trước tối thiểu 3 ngày.",
                SoLuongGioiHan = 50,
                SoLuongDaSuDung = 0
            },
            new KhuyenMai
            {
                MaKM = "KM02",
                TenKM = "Ưu đãi khách đoàn",
                LoaiKM = LoaiKhuyenMai.SO_TIEN_CO_DINH,
                GiaTri = 200000m,
                NgayBatDau = homNay.AddDays(-5),
                NgayKetThuc = homNay.AddDays(90),
                DieuKien = "Áp dụng khi đặt từ 2 phòng trở lên trong cùng 1 phiếu đặt.",
                SoLuongGioiHan = 20,
                SoLuongDaSuDung = 0
            },
            new KhuyenMai
            {
                MaKM = "KM03",
                TenKM = "Khuyến mãi hè",
                LoaiKM = LoaiKhuyenMai.PHAN_TRAM,
                PhanTramKM = 15m,
                GiaTri = 15m,
                NgayBatDau = homNay.AddDays(-90),
                NgayKetThuc = homNay.AddDays(-30),
                DieuKien = "Chỉ áp dụng trong mùa hè, đã kết thúc.",
                SoLuongGioiHan = 100,
                SoLuongDaSuDung = 37
            },
            new KhuyenMai
            {
                MaKM = "KM04",
                TenKM = "Tặng bữa sáng phòng Deluxe",
                LoaiKM = LoaiKhuyenMai.QUA_TANG,
                NgayBatDau = homNay.AddDays(-3),
                NgayKetThuc = homNay.AddDays(45),
                DieuKien = "Tặng 2 suất ăn sáng buffet khi đặt phòng Deluxe từ 2 đêm.",
                SoLuongGioiHan = 30,
                SoLuongDaSuDung = 0
            },
            new KhuyenMai
            {
                MaKM = "KM05",
                TenKM = "Khách quen giảm 100.000đ",
                LoaiKM = LoaiKhuyenMai.SO_TIEN_CO_DINH,
                GiaTri = 100000m,
                NgayBatDau = homNay.AddDays(-20),
                NgayKetThuc = homNay.AddDays(30),
                DieuKien = "Dành cho khách đã từng lưu trú tại khách sạn.",
                SoLuongGioiHan = null,
                SoLuongDaSuDung = 0
            },
            new KhuyenMai
            {
                MaKM = "KM06",
                TenKM = "Ưu đãi mùa lễ hội cuối năm",
                LoaiKM = LoaiKhuyenMai.PHAN_TRAM,
                PhanTramKM = 20m,
                GiaTri = 20m,
                NgayBatDau = homNay.AddDays(60),
                NgayKetThuc = homNay.AddDays(120),
                DieuKien = "Chưa đến thời gian áp dụng - dùng để test lỗi khuyến mãi chưa hiệu lực.",
                SoLuongGioiHan = 40,
                SoLuongDaSuDung = 0
            }
        );
        await context.SaveChangesAsync();
    }

    // Câu hỏi về hình thức thanh toán: dùng chung cho dữ liệu mới và lần cập nhật CSDL cũ (bản cũ còn nhắc thanh toán bằng thẻ).
    private const string TuKhoaThanhToanCu = "thanh toan,hinh thuc thanh toan,tra tien bang gi,quet the,chuyen khoan,tien mat";
    private const string TuKhoaThanhToan = "thanh toan,hinh thuc thanh toan,tra tien bang gi,chuyen khoan,quet ma qr,tien mat";
    private const string CauTraLoiThanhToan =
        "Khách sạn chấp nhận thanh toán bằng tiền mặt hoặc chuyển khoản quét mã QR. Chi tiết từng lần thanh toán đều được ghi nhận trên hệ thống.";

    private static async Task SeedCauHoiCoBanAsync(KLCN060DbContext context)
    {
        if (await context.CauHoiThuongGaps.AnyAsync()) return;

        context.CauHoiThuongGaps.AddRange(
            new CauHoiThuongGap
            {
                TuKhoa = "gio nhan phong,gio check in,checkin,nhan phong luc may gio,gio tra phong,checkout,check out",
                CauTraLoi = "Giờ nhận phòng (check-in) tiêu chuẩn là 14:00 và giờ trả phòng (check-out) tiêu chuẩn là 12:00 trưa. Quý khách cần nhận phòng sớm hoặc trả phòng muộn vui lòng liên hệ lễ tân để được hỗ trợ tùy theo tình trạng phòng trống."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "huy phong,chinh sach huy,hoan coc,hoan tien,huy dat phong",
                CauTraLoi = "Khách có thể hủy phiếu đặt phòng miễn phí trước 24 giờ so với ngày nhận phòng dự kiến. Hủy trong vòng 24 giờ hoặc không đến nhận phòng (no-show), tiền cọc đã thanh toán sẽ không được hoàn lại."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "phu thu,khach thu 3,them nguoi,them khach,qua so nguoi",
                CauTraLoi = "Mỗi loại phòng có số người tiêu chuẩn riêng. Nếu số khách lưu trú vượt quá số người tiêu chuẩn của phòng, khách sạn sẽ áp dụng mức phụ thu tương ứng theo từng loại phòng, được tính vào hóa đơn khi trả phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "wifi,wi-fi,mang internet,internet",
                CauTraLoi = "Khách sạn có wifi miễn phí phủ sóng toàn bộ khuôn viên. Mật khẩu wifi được cung cấp tại quầy lễ tân khi nhận phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = TuKhoaThanhToan,
                CauTraLoi = CauTraLoiThanhToan
            }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedCauHoiThuongGapAsync(KLCN060DbContext context)
    {
        await SeedCauHoiCoBanAsync(context);
        await SeedCauHoiMoRongAsync(context);
    }

    /// <summary>
    /// Bổ sung/cập nhật FAQ cho các chức năng Giai đoạn 3-5 (đặt cọc, hủy, đổi phòng, trả phòng, hóa đơn...).
    /// Chạy mỗi lần khởi động nhưng idempotent: chỉ thêm mục chưa có, và sửa câu trả lời hủy phòng cũ cho đúng chính sách 30 ngày.
    /// </summary>
    private static async Task SeedCauHoiMoRongAsync(KLCN060DbContext context)
    {
        const string tuKhoaHuy = "huy phong,chinh sach huy,hoan coc,hoan tien,huy dat phong";
        const string traLoiHuy =
            "Quý khách có thể hủy phiếu đặt phòng khi chưa nhận phòng. Nếu hủy trước ngày nhận phòng dự kiến từ 30 ngày trở lên, tiền cọc (50% giá trị đặt phòng) được hoàn lại; " +
            "hủy trong vòng dưới 30 ngày hoặc không đến nhận phòng (no-show) thì tiền cọc không được hoàn. Phiếu đã nhận phòng vui lòng liên hệ trực tiếp lễ tân để được hỗ trợ.";

        var faqs = await context.CauHoiThuongGaps.ToListAsync();

        var faqThanhToan = faqs.FirstOrDefault(x => x.TuKhoa == TuKhoaThanhToanCu || x.TuKhoa == TuKhoaThanhToan);
        if (faqThanhToan is not null && (faqThanhToan.TuKhoa != TuKhoaThanhToan || faqThanhToan.CauTraLoi != CauTraLoiThanhToan))
        {
            faqThanhToan.TuKhoa = TuKhoaThanhToan;
            faqThanhToan.CauTraLoi = CauTraLoiThanhToan;
        }

        // Số hotline mẫu cũ "1900 xxxx" -> số thật hiển thị trên Website.
        foreach (var faqCu in faqs.Where(x => x.CauTraLoi.Contains("1900 xxxx")))
            faqCu.CauTraLoi = faqCu.CauTraLoi.Replace("1900 xxxx", "1900 0001");

        var faqHuy = faqs.FirstOrDefault(x => x.TuKhoa == tuKhoaHuy);
        if (faqHuy is not null && faqHuy.CauTraLoi != traLoiHuy)
            faqHuy.CauTraLoi = traLoiHuy;

        var moiRong = new[]
        {
            new CauHoiThuongGap
            {
                TuKhoa = "doi phong,chuyen phong,doi sang phong,phong bi hong",
                CauTraLoi = "Nếu phòng gặp sự cố hoặc quý khách muốn đổi phòng khi đang lưu trú, vui lòng liên hệ lễ tân. Việc đổi phòng tùy thuộc vào tình trạng phòng trống; nếu phòng mới khác hạng phòng, đơn giá những đêm ở phòng mới sẽ tính theo hạng phòng đó."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "tien coc,dat coc,coc bao nhieu,xac nhan coc",
                CauTraLoi = "Tiền đặt cọc bằng 50% tổng tiền phòng dự kiến. Phiếu đặt phòng ở trạng thái chờ xác nhận cho tới khi khách xác nhận đã đặt cọc; sau đó phiếu được xác nhận và phòng được giữ cho quý khách. Tiền cọc sẽ được trừ vào hóa đơn khi trả phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "cach dat phong,dat phong nhu the nao,dat phong online,huong dan dat phong",
                CauTraLoi = "Quý khách chọn ngày nhận - trả phòng, chọn loại phòng và số lượng phòng (cá nhân hoặc đoàn), hệ thống sẽ giữ sẵn phòng trống phù hợp. Sau đó xác nhận đặt cọc để hoàn tất. Quý khách cũng có thể nhờ lễ tân đặt phòng hộ."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "tra phong tre,tra phong muon,tre gio,muon gio tra,phu thu tra phong",
                CauTraLoi = "Giờ trả phòng tiêu chuẩn là 12:00. Nếu quý khách trả phòng trễ và ảnh hưởng đến khách nhận phòng kế tiếp, khách sạn có thể tính phụ thu tương đương 1 đêm; lễ tân sẽ xác nhận với quý khách khi làm thủ tục trả phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "dich vu,giat ui,su dung dich vu,tinh tien dich vu",
                CauTraLoi = "Các dịch vụ quý khách sử dụng trong thời gian lưu trú (giặt ủi và các dịch vụ khác) được lễ tân ghi nhận theo bảng giá tại thời điểm sử dụng và cộng vào hóa đơn khi trả phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "hoa don,xuat hoa don,in hoa don,file pdf,tai hoa don",
                CauTraLoi = "Hóa đơn được lập tự động khi trả phòng, gồm tiền phòng, phụ thu, dịch vụ, khuyến mãi và tiền cọc đã trừ. Quý khách có thể yêu cầu lễ tân xuất hóa đơn dạng PDF để lưu hoặc in."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "do xe,gui xe,bai do xe,cho do xe,parking",
                CauTraLoi = "Khách sạn có bãi đậu xe máy miễn phí và chỗ đậu ô tô trong khuôn viên (số chỗ có hạn, ưu tiên khách đến trước). Quý khách cần chỗ đậu ô tô vui lòng báo lễ tân khi đặt phòng."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "an sang,bua sang,buffet,gio an sang",
                CauTraLoi = "Bữa sáng buffet phục vụ từ 06:30 đến 09:30 hằng ngày, giá 90.000đ/suất, quý khách đăng ký với lễ tân để được cộng vào hóa đơn (một số chương trình khuyến mãi có tặng kèm bữa sáng)."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "tre em,em be,tre nho,giuong phu,them giuong",
                CauTraLoi = "Trẻ em dưới 6 tuổi ngủ chung giường với bố mẹ được miễn phụ thu. Từ 6 tuổi trở lên tính như 1 người thêm theo mức phụ thu của loại phòng. Phòng Triple phù hợp cho gia đình 3 người."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "thu cung,cho meo,mang thu cung,pet",
                CauTraLoi = "Hiện khách sạn chưa nhận thú cưng lưu trú trong phòng. Quý khách vui lòng liên hệ lễ tân nếu cần hỗ trợ gửi thú cưng tại các dịch vụ liên kết gần khách sạn."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "san bay,don san bay,xe dua don,lien khuong,taxi",
                CauTraLoi = "Khách sạn có dịch vụ đưa đón sân bay Liên Khương giá 350.000đ/lượt, quý khách đăng ký trước ít nhất 1 ngày để lễ tân sắp xếp xe."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "dia chi,o dau,lien he,so dien thoai,hotline,vi tri",
                CauTraLoi = "KLCN060 Hotel tọa lạc tại trung tâm TP. Đà Lạt, Lâm Đồng. Hotline hỗ trợ 24/7: 1900 0001. Quý khách cũng có thể nhắn trực tiếp cho chatbot này để được hướng dẫn."
            },
            new CauHoiThuongGap
            {
                TuKhoa = "khuyen mai,uu dai,giam gia,ma giam gia,ma khuyen mai",
                CauTraLoi = "Khách sạn thường xuyên có các chương trình khuyến mãi theo thời gian. Quý khách nhập mã khuyến mãi còn hiệu lực khi đặt phòng để được áp dụng ưu đãi; mã hết hạn hoặc hết lượt sử dụng sẽ không áp dụng được."
            }
        };

        foreach (var faq in moiRong)
        {
            if (!faqs.Any(x => x.TuKhoa == faq.TuKhoa))
                context.CauHoiThuongGaps.Add(faq);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Chuỗi dữ liệu vận hành mẫu: 6 phiếu đặt phòng phủ đủ 5 trạng thái của PhieuDatPhong, trong đó 2 phiếu
    /// đã nhận phòng (1 đang ở, 1 đã trả phòng kèm hóa đơn + thanh toán một phần) - đủ để Đạt/Trọng test
    /// D5-D9/W8-W10 ngay mà không phải tự tạo dữ liệu từ đầu mỗi lần. Ngày tính tương đối theo lúc seed chạy.
    /// </summary>
    private static async Task SeedVongDoiLuuTruMauAsync(KLCN060DbContext context)
    {
        if (await context.PhieuDatPhongs.AnyAsync()) return;

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var bayGio = DateTime.Now;

        // ---- 1) CHO_XAC_NHAN: vua dat, chua xac nhan coc (KH03, Deluxe, 1 phong, +15 ngay nua) ----
        var ngayDon1 = homNay.AddDays(15);
        var ngayTra1 = homNay.AddDays(17);
        var thanhTien1 = 750000m * (ngayTra1.DayNumber - ngayDon1.DayNumber);
        var phieuDat1 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon1:yyyyMMdd}-0001",
            MaKhach = "KH03",
            NgayDat = bayGio.AddDays(-1),
            NgayDonDuKien = ngayDon1,
            NgayTraDuKien = ngayTra1,
            TienCoc = Math.Round(thanhTien1 * 0.5m, 0),
            TrangThai = TrangThaiPhieuDat.CHO_XAC_NHAN,
            LoaiDatPhong = LoaiDatPhong.CA_NHAN
        };
        phieuDat1.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat1.MaPhieuDat, MaPhong = "P303", SoLuong = 1, DonGiaApDung = 750000m, ThanhTien = thanhTien1 });

        // ---- 2) DA_XAC_NHAN: doan 2 phong Standard, da xac nhan coc, ap dung KM02 (+7 ngay nua) ----
        var ngayDon2 = homNay.AddDays(7);
        var ngayTra2 = homNay.AddDays(9);
        var soDem2 = ngayTra2.DayNumber - ngayDon2.DayNumber;
        var phieuDat2 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon2:yyyyMMdd}-0001",
            MaKhach = "KH05",
            NgayDat = bayGio.AddDays(-3),
            NgayDonDuKien = ngayDon2,
            NgayTraDuKien = ngayTra2,
            TrangThai = TrangThaiPhieuDat.DA_XAC_NHAN,
            LoaiDatPhong = LoaiDatPhong.DOAN
        };
        phieuDat2.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat2.MaPhieuDat, MaPhong = "P101", SoLuong = 1, DonGiaApDung = 450000m, ThanhTien = 450000m * soDem2 });
        phieuDat2.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat2.MaPhieuDat, MaPhong = "P102", SoLuong = 1, DonGiaApDung = 450000m, ThanhTien = 450000m * soDem2 });
        var tongTien2 = phieuDat2.ChiTietPhieuDats.Sum(x => x.ThanhTien);
        phieuDat2.TienCoc = Math.Round(tongTien2 * 0.5m, 0);
        phieuDat2.ChiTietKhuyenMais.Add(new ChiTietKhuyenMai { MaPhieuDat = phieuDat2.MaPhieuDat, MaKM = "KM02", TongKhuyenMai = 200000m });

        // ---- 3) DA_HUY: huy som (>=30 ngay), duoc hoan coc (KH06, Triple, +45 ngay nua) ----
        var ngayDon3 = homNay.AddDays(45);
        var ngayTra3 = homNay.AddDays(47);
        var thanhTien3 = 650000m * (ngayTra3.DayNumber - ngayDon3.DayNumber);
        var phieuDat3 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon3:yyyyMMdd}-0001",
            MaKhach = "KH06",
            NgayDat = bayGio.AddDays(-20),
            NgayDonDuKien = ngayDon3,
            NgayTraDuKien = ngayTra3,
            TienCoc = Math.Round(thanhTien3 * 0.5m, 0),
            TrangThai = TrangThaiPhieuDat.DA_HUY,
            LoaiDatPhong = LoaiDatPhong.CA_NHAN
        };
        phieuDat3.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat3.MaPhieuDat, MaPhong = "P402", SoLuong = 1, DonGiaApDung = 650000m, ThanhTien = thanhTien3 });

        // ---- 4) KHONG_DEN: khong den nhan phong, khong hoan coc (KH07, Standard, da qua -20 ngay) ----
        var ngayDon4 = homNay.AddDays(-20);
        var ngayTra4 = homNay.AddDays(-18);
        var thanhTien4 = 450000m * (ngayTra4.DayNumber - ngayDon4.DayNumber);
        var phieuDat4 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon4:yyyyMMdd}-0001",
            MaKhach = "KH07",
            NgayDat = bayGio.AddDays(-25),
            NgayDonDuKien = ngayDon4,
            NgayTraDuKien = ngayTra4,
            TienCoc = Math.Round(thanhTien4 * 0.5m, 0),
            TrangThai = TrangThaiPhieuDat.KHONG_DEN,
            LoaiDatPhong = LoaiDatPhong.CA_NHAN
        };
        phieuDat4.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat4.MaPhieuDat, MaPhong = "P101", SoLuong = 1, DonGiaApDung = 450000m, ThanhTien = thanhTien4 });

        // ---- 5) DA_NHAN_PHONG - dang o: KH02 tai P201 (Superior), nhan tu -2 ngay, du kien tra +3 ngay ----
        var ngayDon5 = homNay.AddDays(-2);
        var ngayTra5 = homNay.AddDays(3);
        var thanhTien5 = 550000m * (ngayTra5.DayNumber - ngayDon5.DayNumber);
        var phieuDat5 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon5:yyyyMMdd}-0001",
            MaKhach = "KH02",
            NgayDat = bayGio.AddDays(-6),
            NgayDonDuKien = ngayDon5,
            NgayTraDuKien = ngayTra5,
            TienCoc = Math.Round(thanhTien5 * 0.5m, 0),
            TrangThai = TrangThaiPhieuDat.DA_NHAN_PHONG,
            LoaiDatPhong = LoaiDatPhong.CA_NHAN
        };
        phieuDat5.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat5.MaPhieuDat, MaPhong = "P201", SoLuong = 1, DonGiaApDung = 550000m, ThanhTien = thanhTien5 });

        var phieuNhan5 = new PhieuNhanPhong
        {
            MaPhieuNhan = $"CI{ngayDon5:yyyyMMdd}-0001",
            MaPhieuDat = phieuDat5.MaPhieuDat,
            // [Khối 8 - K8-5] Backfill MaKhach (cột thêm ở K7-1) cho dữ liệu mẫu — K7-1 chỉ sửa luồng nghiệp vụ (CheckInService), seed tạo phiếu nhận trực tiếp nên bị sót.
            MaKhach = phieuDat5.MaKhach,
            NgayNhan = bayGio.AddDays(-2)
        };
        phieuNhan5.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
        {
            MaPhieuNhan = phieuNhan5.MaPhieuNhan,
            MaPhong = "P201",
            NgayNhan = ngayDon5,
            NgayTra = null,
            NgayTraDuKien = ngayTra5,
            SoNguoi = 2,
            DonGia = 550000m,
            TienPhuThu = 0,
            TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
        });
        phieuNhan5.ChiTietSuDungDVs.Add(new ChiTietSuDungDV { MaPhieuNhan = phieuNhan5.MaPhieuNhan, MaDV = "DV01", SoLuong = 2, ThanhTien = 70000m, NgaySuDung = bayGio.AddDays(-1), NguoiGhi = "letan" });
        phieuNhan5.ChiTietSuDungDVs.Add(new ChiTietSuDungDV { MaPhieuNhan = phieuNhan5.MaPhieuNhan, MaDV = "DV04", SoLuong = 3, ThanhTien = 45000m, NgaySuDung = bayGio, NguoiGhi = "letan" });

        // ---- 6) DA_NHAN_PHONG - da tra phong + hoa don thanh toan mot phan: KH04 tai P203 (Superior) ----
        var ngayDon6 = homNay.AddDays(-8);
        var ngayTra6 = homNay.AddDays(-5);
        var thanhTien6 = 550000m * (ngayTra6.DayNumber - ngayDon6.DayNumber);
        var phieuDat6 = new PhieuDatPhong
        {
            MaPhieuDat = $"BK{ngayDon6:yyyyMMdd}-0001",
            MaKhach = "KH04",
            NgayDat = bayGio.AddDays(-12),
            NgayDonDuKien = ngayDon6,
            NgayTraDuKien = ngayTra6,
            TienCoc = Math.Round(thanhTien6 * 0.5m, 0),
            TrangThai = TrangThaiPhieuDat.DA_NHAN_PHONG,
            LoaiDatPhong = LoaiDatPhong.CA_NHAN
        };
        phieuDat6.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat6.MaPhieuDat, MaPhong = "P203", SoLuong = 1, DonGiaApDung = 550000m, ThanhTien = thanhTien6 });

        var phieuNhan6 = new PhieuNhanPhong
        {
            MaPhieuNhan = $"CI{ngayDon6:yyyyMMdd}-0001",
            MaPhieuDat = phieuDat6.MaPhieuDat,
            // [Khối 8 - K8-5] Backfill MaKhach (cột thêm ở K7-1) cho dữ liệu mẫu — K7-1 chỉ sửa luồng nghiệp vụ (CheckInService), seed tạo phiếu nhận trực tiếp nên bị sót.
            MaKhach = phieuDat6.MaKhach,
            NgayNhan = bayGio.AddDays(-8)
        };
        phieuNhan6.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
        {
            MaPhieuNhan = phieuNhan6.MaPhieuNhan,
            MaPhong = "P203",
            NgayNhan = ngayDon6,
            NgayTra = ngayTra6,
            NgayTraDuKien = ngayTra6,
            SoNguoi = 2,
            DonGia = 550000m,
            TienPhuThu = 0,
            TrangThai = TrangThaiChiTietPhieuNhan.DA_TRA_PHONG
        });
        phieuNhan6.ChiTietSuDungDVs.Add(new ChiTietSuDungDV { MaPhieuNhan = phieuNhan6.MaPhieuNhan, MaDV = "DV02", SoLuong = 1, ThanhTien = 130000m, NgaySuDung = bayGio.AddDays(-6), NguoiGhi = "letan" });

        var maHD6 = $"HD{ngayTra6:yyyyMMdd}-0001";
        var tongTienPhong6 = thanhTien6;
        var tongTienDV6 = 130000m;
        var tienDaCoc6 = phieuDat6.TienCoc;
        var conPhaiThu6 = tongTienPhong6 + tongTienDV6 - tienDaCoc6;
        var hoaDon6 = new HoaDon
        {
            MaHD = maHD6,
            MaPhieuNhan = phieuNhan6.MaPhieuNhan,
            MaNV = "NV02",
            NgayLap = bayGio.AddDays(-5),
            TienPhong = tongTienPhong6,
            TienDV = tongTienDV6,
            PhuThu = 0,
            TienDaCoc = tienDaCoc6,
            TongTien = conPhaiThu6,
            HinhThucThanhToan = HinhThucThanhToan.TIEN_MAT,
            TrangThaiThanhToan = TrangThaiThanhToan.THANH_TOAN_MOT_PHAN
        };
        hoaDon6.ChiTietHoaDons.Add(new ChiTietHoaDon { MaHD = maHD6, LoaiKhoanMuc = LoaiKhoanMuc.TIEN_PHONG, MoTa = "Tiền phòng P203 (Superior) x3 đêm", SoLuong = 3, DonGia = 550000m, ThanhTien = tongTienPhong6 });
        hoaDon6.ChiTietHoaDons.Add(new ChiTietHoaDon { MaHD = maHD6, LoaiKhoanMuc = LoaiKhoanMuc.DICH_VU, MoTa = "Thuê xe máy ngày thường", SoLuong = 1, DonGia = 130000m, ThanhTien = tongTienDV6 });
        hoaDon6.ChiTietThanhToans.Add(new ChiTietThanhToan
        {
            MaHD = maHD6,
            HinhThucThanhToan = HinhThucThanhToan.TIEN_MAT,
            SoTien = 500000m,
            ThoiGianThanhToan = bayGio.AddDays(-5).AddMinutes(5),
            MaTaiKhoanThuNgan = "NV02"
        });
        phieuNhan6.HoaDons.Add(hoaDon6);

        context.PhieuDatPhongs.AddRange(phieuDat1, phieuDat2, phieuDat3, phieuDat4, phieuDat5, phieuDat6);
        context.PhieuNhanPhongs.AddRange(phieuNhan5, phieuNhan6);

        // Đồng bộ SoLuongDaSuDung của KM02 với việc phieuDat2 đã áp dụng khuyến mãi này ngay từ đầu.
        var km02 = await context.KhuyenMais.FirstOrDefaultAsync(x => x.MaKM == "KM02");
        if (km02 is not null) km02.SoLuongDaSuDung += 1;

        await context.SaveChangesAsync();
    }
}
