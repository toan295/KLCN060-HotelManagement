using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietPhieuNhanConfiguration : IEntityTypeConfiguration<ChiTietPhieuNhan>
{
    public void Configure(EntityTypeBuilder<ChiTietPhieuNhan> entity)
    {
        entity.HasKey(x => new { x.MaPhieuNhan, x.MaPhong });

        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.NgayNhan).HasColumnType("date").IsRequired();
        entity.Property(x => x.NgayTra).HasColumnType("date");
        entity.Property(x => x.SoNguoi).IsRequired();
        entity.Property(x => x.DonGia).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TienPhuThu).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);

        entity.Property(x => x.TrangThai)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.HasOne(x => x.PhieuNhanPhong)
            .WithMany(x => x.ChiTietPhieuNhans)
            .HasForeignKey(x => x.MaPhieuNhan)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Phong)
            .WithMany(x => x.ChiTietPhieuNhans)
            .HasForeignKey(x => x.MaPhong)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
