using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class NhatKyBanGiaoCaConfiguration : IEntityTypeConfiguration<NhatKyBanGiaoCa>
{
    public void Configure(EntityTypeBuilder<NhatKyBanGiaoCa> entity)
    {
        entity.HasKey(x => x.MaBanGiao);
        entity.Property(x => x.MaBanGiao).ValueGeneratedOnAdd();

        entity.Property(x => x.MaTaiKhoanGiao).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaTaiKhoanNhan).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.ThoiGianBanGiao).IsRequired().HasDefaultValueSql("GETDATE()");
        entity.Property(x => x.TongTienMatDauCa).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TongTienMatCuoiCa).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.SoLuongPhieuTrongCa).IsRequired().HasDefaultValue(0);
        entity.Property(x => x.GhiChu).HasMaxLength(500);

        entity.HasOne(x => x.TaiKhoanGiao)
            .WithMany(x => x.NhatKyBanGiaoCaGiaos)
            .HasForeignKey(x => x.MaTaiKhoanGiao)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.TaiKhoanNhan)
            .WithMany(x => x.NhatKyBanGiaoCaNhans)
            .HasForeignKey(x => x.MaTaiKhoanNhan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
