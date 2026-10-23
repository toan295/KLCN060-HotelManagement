using KLCN060.Domain.Enums;
using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class SuCoPhongConfiguration : IEntityTypeConfiguration<SuCoPhong>
{
    public void Configure(EntityTypeBuilder<SuCoPhong> entity)
    {
        entity.HasKey(x => x.MaSuCo);
        entity.Property(x => x.MaSuCo).ValueGeneratedOnAdd();
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.Loai).HasConversion<string>().HasMaxLength(30).IsUnicode(false).IsRequired();
        entity.Property(x => x.TieuDe).HasMaxLength(150).IsRequired();
        entity.Property(x => x.MoTa).HasMaxLength(1000);
        entity.Property(x => x.MucDo).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiBao).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiXuLy).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.NguoiDong).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.GhiChuXuLy).HasMaxLength(500);

        entity.HasOne(x => x.Phong).WithMany().HasForeignKey(x => x.MaPhong).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.MaPhong, x.TrangThai });
        entity.HasIndex(x => x.TrangThai);
    }
}

public class VatDungConfiguration : IEntityTypeConfiguration<VatDung>
{
    public void Configure(EntityTypeBuilder<VatDung> entity)
    {
        entity.HasKey(x => x.MaVatDung);
        entity.Property(x => x.MaVatDung).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.TenVatDung).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Nhom).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.DonViTinh).HasMaxLength(20).IsRequired();
        entity.Property(x => x.SoLuongChuan).IsRequired();
        entity.Property(x => x.CachTinh).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(CachTinhDinhMuc.MOI_PHONG);
        entity.Property(x => x.MaDV).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.HoatDong).IsRequired().HasDefaultValue(true);

        entity.HasOne(x => x.DichVu).WithMany().HasForeignKey(x => x.MaDV).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.TenVatDung).IsUnique();
    }
}

public class VatDungLoaiPhongConfiguration : IEntityTypeConfiguration<VatDungLoaiPhong>
{
    public void Configure(EntityTypeBuilder<VatDungLoaiPhong> entity)
    {
        entity.HasKey(x => new { x.MaVatDung, x.MaLoai });
        entity.Property(x => x.MaVatDung).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.MaLoai).HasMaxLength(10).IsUnicode(false);
        entity.HasOne(x => x.VatDung).WithMany(x => x.LoaiPhongApDungs).HasForeignKey(x => x.MaVatDung).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.LoaiPhong).WithMany().HasForeignKey(x => x.MaLoai).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PhieuKiemPhongConfiguration : IEntityTypeConfiguration<PhieuKiemPhong>
{
    public void Configure(EntityTypeBuilder<PhieuKiemPhong> entity)
    {
        entity.HasKey(x => x.MaPhieuKiem);
        entity.Property(x => x.MaPhieuKiem).ValueGeneratedOnAdd();
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.NguoiKiem).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.GhiChu).HasMaxLength(500);

        entity.HasOne(x => x.Phong).WithMany().HasForeignKey(x => x.MaPhong).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.MaPhong, x.ThoiGian });
    }
}

public class ChiTietKiemPhongConfiguration : IEntityTypeConfiguration<ChiTietKiemPhong>
{
    public void Configure(EntityTypeBuilder<ChiTietKiemPhong> entity)
    {
        entity.HasKey(x => x.MaChiTiet);
        entity.Property(x => x.MaChiTiet).ValueGeneratedOnAdd();
        entity.Property(x => x.MaVatDung).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.LyDo).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.GhiChu).HasMaxLength(200);
        entity.Property(x => x.ThanhTien).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TrangThaiTinhPhi).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiXuLy).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.LyDoBoQua).HasMaxLength(300);

        entity.HasOne(x => x.PhieuKiemPhong).WithMany(x => x.ChiTiets).HasForeignKey(x => x.MaPhieuKiem).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.VatDung).WithMany(x => x.ChiTietKiemPhongs).HasForeignKey(x => x.MaVatDung).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.TrangThaiTinhPhi);
    }
}

public class DoBoQuenConfiguration : IEntityTypeConfiguration<DoBoQuen>
{
    public void Configure(EntityTypeBuilder<DoBoQuen> entity)
    {
        entity.HasKey(x => x.MaDo);
        entity.Property(x => x.MaDo).ValueGeneratedOnAdd();
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.KhuVuc).HasMaxLength(100);
        entity.Property(x => x.Loai).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.MoTa).HasMaxLength(500).IsRequired();
        entity.Property(x => x.NguoiNhat).HasMaxLength(100).IsRequired();
        entity.Property(x => x.NguoiGhiNhan).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.ViTriLuuGiu).HasMaxLength(100);
        entity.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.NguoiNhanLai).HasMaxLength(100);
        entity.Property(x => x.GiayToNguoiNhan).HasMaxLength(50);
        entity.Property(x => x.NguoiTra).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.GhiChuTra).HasMaxLength(300);
        entity.Property(x => x.NguoiThanhLy).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.LyDoThanhLy).HasMaxLength(300);

        entity.HasOne(x => x.Phong).WithMany().HasForeignKey(x => x.MaPhong).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.TrangThai);
        entity.HasIndex(x => x.ThoiGianPhatHien);
    }
}

public class AnhDoBoQuenConfiguration : IEntityTypeConfiguration<AnhDoBoQuen>
{
    public void Configure(EntityTypeBuilder<AnhDoBoQuen> entity)
    {
        entity.HasKey(x => x.MaAnh);
        entity.Property(x => x.MaAnh).ValueGeneratedOnAdd();
        entity.Property(x => x.TenFile).HasMaxLength(80).IsUnicode(false).IsRequired();

        entity.HasOne(x => x.DoBoQuen).WithMany(x => x.Anhs).HasForeignKey(x => x.MaDo).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => x.MaDo);
    }
}

public class PhieuDonPhongConfiguration : IEntityTypeConfiguration<PhieuDonPhong>
{
    public void Configure(EntityTypeBuilder<PhieuDonPhong> entity)
    {
        entity.HasKey(x => x.MaPhieuDon);
        entity.Property(x => x.MaPhieuDon).ValueGeneratedOnAdd();
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiDon).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiPhanCong).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.TinhTrangBanDau).HasConversion<string>().HasMaxLength(5).IsUnicode(false).IsRequired();
        entity.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();

        entity.HasOne(x => x.Phong).WithMany().HasForeignKey(x => x.MaPhong).OnDelete(DeleteBehavior.Restrict);
        // Mỗi phòng chỉ có một phiếu dọn đang mở: hai người cùng bấm "nhận dọn" thì chỉ một người thành công.
        entity.HasIndex(x => x.MaPhong).IsUnique().HasFilter("[TrangThai] = 'DANG_DON'");
        entity.HasIndex(x => x.ThoiGianXong);
    }
}
