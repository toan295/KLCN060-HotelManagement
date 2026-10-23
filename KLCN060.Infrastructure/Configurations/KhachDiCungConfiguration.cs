using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class KhachDiCungConfiguration : IEntityTypeConfiguration<KhachDiCung>
{
    public void Configure(EntityTypeBuilder<KhachDiCung> entity)
    {
        entity.HasKey(x => x.MaKhachDiCung);
        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.HoTen).HasMaxLength(100).IsRequired();
        entity.Property(x => x.CCCD).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.NguoiThem).HasMaxLength(50).IsUnicode(false);

        entity.HasOne(x => x.PhieuDatPhong)
            .WithMany(x => x.KhachDiCungs)
            .HasForeignKey(x => x.MaPhieuDat)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => x.MaPhieuDat);
    }
}
