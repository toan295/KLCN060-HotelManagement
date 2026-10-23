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

        entity.Property(x => x.NguonDat)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired()
            .HasDefaultValue(Domain.Enums.NguonDatPhong.QUAY_LE_TAN);
        entity.Property(x => x.LyDoHuy).HasMaxLength(500);
        entity.Property(x => x.HuyBoi).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.SoTienHoanCoc).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
        entity.Property(x => x.TienCocGiuLai).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
        entity.Property(x => x.HinhThucCoc).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.NguoiNhanCoc).HasMaxLength(50).IsUnicode(false);

        entity.HasOne(x => x.Khach)
            .WithMany(x => x.PhieuDatPhongs)
            .HasForeignKey(x => x.MaKhach)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
