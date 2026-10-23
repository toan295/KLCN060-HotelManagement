using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

public class KLCN060DbContext : DbContext
{
    public KLCN060DbContext(DbContextOptions<KLCN060DbContext> options) : base(options)
    {
    }

    // Nhóm 1 — Tài khoản, nhân viên, khách hàng, phân quyền
    public DbSet<VaiTro> VaiTros => Set<VaiTro>();
    public DbSet<Quyen> Quyens => Set<Quyen>();
    public DbSet<VaiTroQuyen> VaiTroQuyens => Set<VaiTroQuyen>();
    public DbSet<NhanVien> NhanViens => Set<NhanVien>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<Khach> Khachs => Set<Khach>();

    // Nhóm 2 — Phòng, loại phòng, cơ sở vật chất
    public DbSet<LoaiPhong> LoaiPhongs => Set<LoaiPhong>();
    public DbSet<Phong> Phongs => Set<Phong>();
    public DbSet<LichSuTinhTrangPhong> LichSuTinhTrangPhongs => Set<LichSuTinhTrangPhong>();
    public DbSet<QuyTacGia> QuyTacGias => Set<QuyTacGia>();
    public DbSet<CoSoVatChat> CoSoVatChats => Set<CoSoVatChat>();

    // Nhóm 3 — Đặt phòng, nhận phòng, đổi phòng
    public DbSet<PhieuDatPhong> PhieuDatPhongs => Set<PhieuDatPhong>();
    public DbSet<ChiTietPhieuDat> ChiTietPhieuDats => Set<ChiTietPhieuDat>();
    public DbSet<GiaDemDatPhong> GiaDemDatPhongs => Set<GiaDemDatPhong>();
    public DbSet<PhieuNhanPhong> PhieuNhanPhongs => Set<PhieuNhanPhong>();
    public DbSet<ChiTietPhieuNhan> ChiTietPhieuNhans => Set<ChiTietPhieuNhan>();
    public DbSet<PhieuDoiPhong> PhieuDoiPhongs => Set<PhieuDoiPhong>();

    // Nhóm 4 — Dịch vụ
    public DbSet<DichVu> DichVus => Set<DichVu>();
    public DbSet<ChiTietSuDungDV> ChiTietSuDungDVs => Set<ChiTietSuDungDV>();
    public DbSet<ChiTietDichVu> ChiTietDichVus => Set<ChiTietDichVu>();
    public DbSet<GoiDichVu> GoiDichVus => Set<GoiDichVu>();
    public DbSet<ChiTietGoiDichVu> ChiTietGoiDichVus => Set<ChiTietGoiDichVu>();

    // Nhóm 5 — Khuyến mãi
    public DbSet<KhuyenMai> KhuyenMais => Set<KhuyenMai>();
    public DbSet<ChiTietKhuyenMai> ChiTietKhuyenMais => Set<ChiTietKhuyenMai>();

    // Nhóm 6 — Hóa đơn và thanh toán
    public DbSet<HoaDon> HoaDons => Set<HoaDon>();
    public DbSet<HoaDonDienTu> HoaDonDienTus => Set<HoaDonDienTu>();
    public DbSet<ChiTietHoaDon> ChiTietHoaDons => Set<ChiTietHoaDon>();
    public DbSet<ChiTietThanhToan> ChiTietThanhToans => Set<ChiTietThanhToan>();

    // Nhóm 7 — Nhật ký vận hành
    public DbSet<NhatKyThaoTac> NhatKyThaoTacs => Set<NhatKyThaoTac>();
    public DbSet<LichSuSaoLuu> LichSuSaoLuus => Set<LichSuSaoLuu>();

    // Người ở cùng khai báo theo phiếu đặt phòng (lễ tân thêm/xóa).
    public DbSet<KhachDiCung> KhachDiCungs => Set<KhachDiCung>();

    // Buồng phòng: sự cố phòng, danh mục vật dụng + kiểm phòng/minibar, đồ khách bỏ quên, việc dọn phòng.
    public DbSet<SuCoPhong> SuCoPhongs => Set<SuCoPhong>();
    public DbSet<VatDung> VatDungs => Set<VatDung>();
    public DbSet<VatDungLoaiPhong> VatDungLoaiPhongs => Set<VatDungLoaiPhong>();
    public DbSet<PhieuKiemPhong> PhieuKiemPhongs => Set<PhieuKiemPhong>();
    public DbSet<ChiTietKiemPhong> ChiTietKiemPhongs => Set<ChiTietKiemPhong>();
    public DbSet<DoBoQuen> DoBoQuens => Set<DoBoQuen>();
    public DbSet<AnhDoBoQuen> AnhDoBoQuens => Set<AnhDoBoQuen>();
    public DbSet<PhieuDonPhong> PhieuDonPhongs => Set<PhieuDonPhong>();

    public DbSet<CanhBaoTheoDoi> CanhBaoTheoDois => Set<CanhBaoTheoDoi>();

    // Quên mật khẩu: các mã đặt lại (chỉ lưu băm) còn/ hết hiệu lực.
    public DbSet<YeuCauDatLaiMatKhau> YeuCauDatLaiMatKhaus => Set<YeuCauDatLaiMatKhau>();

    // Thanh toán chuyển khoản mô phỏng (yêu cầu thanh toán + nhật ký thông báo từ ngân hàng giả lập).
    public DbSet<GiaoDichChuyenKhoan> GiaoDichChuyenKhoans => Set<GiaoDichChuyenKhoan>();
    public DbSet<ThongBaoNganHang> ThongBaoNganHangs => Set<ThongBaoNganHang>();

    // Bảng cấu hình tĩnh cho chatbot (Giai đoạn 2, Mục 6.13) — không thuộc 24 bảng nghiệp vụ.
    public DbSet<CauHoiThuongGap> CauHoiThuongGaps => Set<CauHoiThuongGap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KLCN060DbContext).Assembly);
    }
}
