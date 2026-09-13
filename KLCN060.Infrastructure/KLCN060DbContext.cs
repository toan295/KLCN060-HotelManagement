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
    public DbSet<CoSoVatChat> CoSoVatChats => Set<CoSoVatChat>();

    // Nhóm 3 — Đặt phòng, nhận phòng, đổi phòng
    public DbSet<PhieuDatPhong> PhieuDatPhongs => Set<PhieuDatPhong>();
    public DbSet<ChiTietPhieuDat> ChiTietPhieuDats => Set<ChiTietPhieuDat>();
    public DbSet<PhieuNhanPhong> PhieuNhanPhongs => Set<PhieuNhanPhong>();
    public DbSet<ChiTietPhieuNhan> ChiTietPhieuNhans => Set<ChiTietPhieuNhan>();
    public DbSet<PhieuDoiPhong> PhieuDoiPhongs => Set<PhieuDoiPhong>();

    // Nhóm 4 — Dịch vụ
    public DbSet<DichVu> DichVus => Set<DichVu>();
    public DbSet<ChiTietSuDungDV> ChiTietSuDungDVs => Set<ChiTietSuDungDV>();

    // Nhóm 5 — Khuyến mãi
    public DbSet<KhuyenMai> KhuyenMais => Set<KhuyenMai>();
    public DbSet<ChiTietKhuyenMai> ChiTietKhuyenMais => Set<ChiTietKhuyenMai>();

    // Nhóm 6 — Hóa đơn và thanh toán
    public DbSet<HoaDon> HoaDons => Set<HoaDon>();
    public DbSet<ChiTietHoaDon> ChiTietHoaDons => Set<ChiTietHoaDon>();
    public DbSet<ChiTietThanhToan> ChiTietThanhToans => Set<ChiTietThanhToan>();

    // Nhóm 7 — Nhật ký vận hành
    public DbSet<NhatKyThaoTac> NhatKyThaoTacs => Set<NhatKyThaoTac>();
    public DbSet<LichSuSaoLuu> LichSuSaoLuus => Set<LichSuSaoLuu>();
    public DbSet<NhatKyBanGiaoCa> NhatKyBanGiaoCas => Set<NhatKyBanGiaoCa>();

    // Bảng cấu hình tĩnh cho chatbot (Giai đoạn 2, Mục 6.13) — không thuộc 24 bảng nghiệp vụ.
    public DbSet<CauHoiThuongGap> CauHoiThuongGaps => Set<CauHoiThuongGap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KLCN060DbContext).Assembly);
    }
}
