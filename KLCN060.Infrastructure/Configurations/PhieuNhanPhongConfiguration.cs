using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class PhieuNhanPhongConfiguration : IEntityTypeConfiguration<PhieuNhanPhong>
{
    public void Configure(EntityTypeBuilder<PhieuNhanPhong> entity)
    {
        entity.HasKey(x => x.MaPhieuNhan);
        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false);

        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.NgayNhan).IsRequired();

        entity.HasOne(x => x.PhieuDatPhong)
            .WithOne(x => x.PhieuNhanPhong)
            .HasForeignKey<PhieuNhanPhong>(x => x.MaPhieuDat)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
