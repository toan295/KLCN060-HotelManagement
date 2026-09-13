using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

/// <summary>
/// Seed dữ liệu bắt buộc của Giai đoạn 1 — chạy tại startup, idempotent
/// (chỉ chèn khi bảng tương ứng đang rỗng). Không seed KhuyenMai / CoSoVatChat (không bắt buộc).
/// </summary>
public static class DataSeeder
{
    private const string MatKhauMacDinh = "Klcn060@2026";

    public static async Task SeedAsync(KLCN060DbContext context)
    {
        await SeedVaiTroAsync(context);
        await SeedQuyenAsync(context);
        await SeedVaiTroQuyenAsync(context);
        await SeedNhanVienAsync(context);
        await SeedKhachAsync(context);
        await SeedTaiKhoanAsync(context);
        await SeedLoaiPhongAsync(context);
        await SeedPhongAsync(context);
        await SeedDichVuAsync(context);
        await SeedCauHoiThuongGapAsync(context);
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

    private static async Task SeedQuyenAsync(KLCN060DbContext context)
    {
        if (await context.Quyens.AnyAsync()) return;

        context.Quyens.AddRange(
            new Quyen { TenQuyen = "XEM_BAO_CAO", NhomChucNang = "BaoCao" },
            new Quyen { TenQuyen = "QUAN_LY_PHONG", NhomChucNang = "Phong" },
            new Quyen { TenQuyen = "QUAN_LY_KHUYEN_MAI", NhomChucNang = "KhuyenMai" },
            new Quyen { TenQuyen = "THAO_TAC_LE_TAN", NhomChucNang = "LeTan" },
            new Quyen { TenQuyen = "QUAN_TRI_HE_THONG", NhomChucNang = "HeThong" }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedVaiTroQuyenAsync(KLCN060DbContext context)
    {
        if (await context.VaiTroQuyens.AnyAsync()) return;

        var vaiTros = await context.VaiTros.ToDictionaryAsync(x => x.TenVaiTro, x => x.MaVaiTro);
        var quyens = await context.Quyens.ToDictionaryAsync(x => x.TenQuyen, x => x.MaQuyen);

        var entries = new List<VaiTroQuyen>();
        // QUAN_LY -> cả 5 quyền
        entries.AddRange(quyens.Values.Select(maQuyen => new VaiTroQuyen { MaVaiTro = vaiTros["QUAN_LY"], MaQuyen = maQuyen }));
        // LE_TAN -> THAO_TAC_LE_TAN
        entries.Add(new VaiTroQuyen { MaVaiTro = vaiTros["LE_TAN"], MaQuyen = quyens["THAO_TAC_LE_TAN"] });
        // KE_TOAN -> XEM_BAO_CAO
        entries.Add(new VaiTroQuyen { MaVaiTro = vaiTros["KE_TOAN"], MaQuyen = quyens["XEM_BAO_CAO"] });

        context.VaiTroQuyens.AddRange(entries);
        await context.SaveChangesAsync();
    }

    private static async Task SeedNhanVienAsync(KLCN060DbContext context)
    {
        if (await context.NhanViens.AnyAsync()) return;

        var vaiTros = await context.VaiTros.ToDictionaryAsync(x => x.TenVaiTro, x => x.MaVaiTro);

        context.NhanViens.AddRange(
            new NhanVien { MaNV = "NV01", HoTen = "Nguyễn Văn Quản", SoDT = "0900000001", ChucVu = "Quản lý", MaVaiTro = vaiTros["QUAN_LY"] },
            new NhanVien { MaNV = "NV02", HoTen = "Trần Thị Tân", SoDT = "0900000002", ChucVu = "Lễ tân", MaVaiTro = vaiTros["LE_TAN"] },
            new NhanVien { MaNV = "NV03", HoTen = "Lê Văn Toán", SoDT = "0900000003", ChucVu = "Kế toán", MaVaiTro = vaiTros["KE_TOAN"] },
            new NhanVien { MaNV = "NV04", HoTen = "Phạm Thị Phòng", SoDT = "0900000004", ChucVu = "Buồng phòng", MaVaiTro = vaiTros["BUONG_PHONG"] }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedKhachAsync(KLCN060DbContext context)
    {
        if (await context.Khachs.AnyAsync()) return;

        context.Khachs.Add(new Khach { MaKhach = "KH01", HoTen = "Đỗ Thị Hương", SoDT = "0905123456" });
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
            new TaiKhoan { TenDN = "0905123456", MatKhau = Hash(), LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG, MaKhach = "KH01", TrangThai = TrangThaiTaiKhoan.HOAT_DONG }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedLoaiPhongAsync(KLCN060DbContext context)
    {
        if (await context.LoaiPhongs.AnyAsync()) return;

        context.LoaiPhongs.AddRange(
            new LoaiPhong { MaLoai = "LP01", TenLoai = "Standard", SoNguoiTieuChuan = 2, DonGia = 450000m, PhuThu = 200000m },
            new LoaiPhong { MaLoai = "LP02", TenLoai = "Superior", SoNguoiTieuChuan = 2, DonGia = 550000m, PhuThu = 200000m },
            new LoaiPhong { MaLoai = "LP03", TenLoai = "Deluxe", SoNguoiTieuChuan = 2, DonGia = 750000m, PhuThu = 200000m },
            new LoaiPhong { MaLoai = "LP04", TenLoai = "Triple", SoNguoiTieuChuan = 3, DonGia = 650000m, PhuThu = 200000m }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedPhongAsync(KLCN060DbContext context)
    {
        if (await context.Phongs.AnyAsync()) return;

        context.Phongs.AddRange(
            new Phong { MaPhong = "P104", TenPhong = "104", Tang = 1, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP01" },
            new Phong { MaPhong = "P204", TenPhong = "204", Tang = 2, TinhTrang = TinhTrangPhong.OC, MaLoai = "LP02" },
            new Phong { MaPhong = "P106", TenPhong = "106", Tang = 1, TinhTrang = TinhTrangPhong.OOO, MaLoai = "LP01" },
            new Phong { MaPhong = "P301", TenPhong = "301", Tang = 3, TinhTrang = TinhTrangPhong.VC, MaLoai = "LP03" },
            new Phong { MaPhong = "P302", TenPhong = "302", Tang = 3, TinhTrang = TinhTrangPhong.VD, MaLoai = "LP04" }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedDichVuAsync(KLCN060DbContext context)
    {
        if (await context.DichVus.AnyAsync()) return;

        context.DichVus.AddRange(
            new DichVu { MaDV = "DV01", TenDV = "Giặt ủi", GiaDV = 35000m, DonViTinh = "kg" },
            new DichVu { MaDV = "DV02", TenDV = "Thuê xe máy ngày thường", GiaDV = 130000m, DonViTinh = "xe/ngày" },
            new DichVu { MaDV = "DV03", TenDV = "Thuê xe máy ngày lễ", GiaDV = 180000m, DonViTinh = "xe/ngày" },
            new DichVu { MaDV = "DV04", TenDV = "Nước ngọt", GiaDV = 15000m, DonViTinh = "lon" }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedCauHoiThuongGapAsync(KLCN060DbContext context)
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
                TuKhoa = "thanh toan,hinh thuc thanh toan,tra tien bang gi,quet the,chuyen khoan,tien mat",
                CauTraLoi = "Khách sạn chấp nhận thanh toán bằng tiền mặt, chuyển khoản hoặc quẹt thẻ. Quý khách có thể thanh toán một phần hoặc toàn bộ hóa đơn, chi tiết từng lần thanh toán đều được ghi nhận trên hệ thống."
            }
        );
        await context.SaveChangesAsync();
    }
}
