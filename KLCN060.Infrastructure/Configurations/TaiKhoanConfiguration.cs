using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class TaiKhoanConfiguration : IEntityTypeConfiguration<TaiKhoan>
{
    public void Configure(EntityTypeBuilder<TaiKhoan> entity)
    {
        entity.HasKey(x => x.TenDN);
        entity.Property(x => x.TenDN).HasMaxLength(50).IsUnicode(false);

        entity.Property(x => x.MatKhau).HasMaxLength(255).IsUnicode(false).IsRequired();

        entity.Property(x => x.LoaiTaiKhoan)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.MaNV).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.MaKhach).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TrangThai)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(Domain.Enums.TrangThaiTaiKhoan.HOAT_DONG);

        entity.Property(x => x.SoLanDangNhapSai).IsRequired().HasDefaultValue(0);
        entity.Property(x => x.RefreshToken).HasMaxLength(255);

        entity.HasOne(x => x.NhanVien)
            .WithOne(x => x.TaiKhoan)
            .HasForeignKey<TaiKhoan>(x => x.MaNV)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Khach)
            .WithOne(x => x.TaiKhoan)
            .HasForeignKey<TaiKhoan>(x => x.MaKhach)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
