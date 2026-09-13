using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietPhieuDatConfiguration : IEntityTypeConfiguration<ChiTietPhieuDat>
{
    public void Configure(EntityTypeBuilder<ChiTietPhieuDat> entity)
    {
        entity.HasKey(x => new { x.MaPhieuDat, x.MaPhong });

        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.SoLuong).IsRequired().HasDefaultValue(1);
        entity.Property(x => x.DonGiaApDung).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.ThanhTien).HasColumnType("decimal(18,2)").IsRequired();

        entity.HasOne(x => x.PhieuDatPhong)
            .WithMany(x => x.ChiTietPhieuDats)
            .HasForeignKey(x => x.MaPhieuDat)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Phong)
            .WithMany(x => x.ChiTietPhieuDats)
            .HasForeignKey(x => x.MaPhong)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
