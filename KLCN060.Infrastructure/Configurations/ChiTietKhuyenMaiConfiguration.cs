using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietKhuyenMaiConfiguration : IEntityTypeConfiguration<ChiTietKhuyenMai>
{
    public void Configure(EntityTypeBuilder<ChiTietKhuyenMai> entity)
    {
        entity.HasKey(x => new { x.MaPhieuDat, x.MaKM });

        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.MaKM).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TongKhuyenMai).HasColumnType("decimal(18,2)").IsRequired();

        entity.HasOne(x => x.PhieuDatPhong)
            .WithMany(x => x.ChiTietKhuyenMais)
            .HasForeignKey(x => x.MaPhieuDat)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.KhuyenMai)
            .WithMany(x => x.ChiTietKhuyenMais)
            .HasForeignKey(x => x.MaKM)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
