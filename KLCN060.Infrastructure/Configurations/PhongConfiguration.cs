using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class PhongConfiguration : IEntityTypeConfiguration<Phong>
{
    public void Configure(EntityTypeBuilder<Phong> entity)
    {
        entity.HasKey(x => x.MaPhong);
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TenPhong).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.Tang).IsRequired();

        entity.Property(x => x.TinhTrang)
            .HasConversion<string>()
            .HasMaxLength(5)
            .IsUnicode(false)
            .IsRequired();

        entity.Property(x => x.MaLoai).HasMaxLength(10).IsUnicode(false).IsRequired();

        entity.HasOne(x => x.LoaiPhong)
            .WithMany(x => x.Phongs)
            .HasForeignKey(x => x.MaLoai)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
