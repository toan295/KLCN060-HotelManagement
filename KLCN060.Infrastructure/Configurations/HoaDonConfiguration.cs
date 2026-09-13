using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class HoaDonConfiguration : IEntityTypeConfiguration<HoaDon>
{
    public void Configure(EntityTypeBuilder<HoaDon> entity)
    {
        entity.HasKey(x => x.MaHD);
        entity.Property(x => x.MaHD).HasMaxLength(15).IsUnicode(false);

        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaNV).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.NgayLap).IsRequired();

        entity.Property(x => x.TienPhong).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TienDV).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.PhuThu).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
        entity.Property(x => x.TienDaCoc).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TongTien).HasColumnType("decimal(18,2)").IsRequired();

        entity.Property(x => x.HinhThucThanhToan)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.TrangThaiThanhToan)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(Domain.Enums.TrangThaiThanhToan.CHUA_THANH_TOAN);

        entity.HasOne(x => x.PhieuNhanPhong)
            .WithMany(x => x.HoaDons)
            .HasForeignKey(x => x.MaPhieuNhan)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.NhanVien)
            .WithMany(x => x.HoaDons)
            .HasForeignKey(x => x.MaNV)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
