using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class PhieuDatPhongConfiguration : IEntityTypeConfiguration<PhieuDatPhong>
{
    public void Configure(EntityTypeBuilder<PhieuDatPhong> entity)
    {
        entity.HasKey(x => x.MaPhieuDat);
        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false);

        entity.Property(x => x.MaKhach).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.NgayDat).IsRequired();
        entity.Property(x => x.NgayDonDuKien).HasColumnType("date").IsRequired();
        entity.Property(x => x.NgayTraDuKien).HasColumnType("date").IsRequired();
        entity.Property(x => x.TienCoc).HasColumnType("decimal(18,2)").IsRequired();

        entity.Property(x => x.TrangThai)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.LoaiDatPhong)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        entity.HasOne(x => x.Khach)
            .WithMany(x => x.PhieuDatPhongs)
            .HasForeignKey(x => x.MaKhach)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
