using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietThanhToanConfiguration : IEntityTypeConfiguration<ChiTietThanhToan>
{
    public void Configure(EntityTypeBuilder<ChiTietThanhToan> entity)
    {
        entity.HasKey(x => x.MaThanhToan);
        entity.Property(x => x.MaThanhToan).ValueGeneratedOnAdd();

        entity.Property(x => x.MaHD).HasMaxLength(15).IsUnicode(false).IsRequired();

        entity.Property(x => x.HinhThucThanhToan)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.SoTien).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.ThoiGianThanhToan).IsRequired();
        entity.Property(x => x.MaTaiKhoanThuNgan).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaGiaoDich).HasMaxLength(100).IsUnicode(false);

        entity.HasOne(x => x.HoaDon)
            .WithMany(x => x.ChiTietThanhToans)
            .HasForeignKey(x => x.MaHD)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.NhanVienThuNgan)
            .WithMany(x => x.ChiTietThanhToans)
            .HasForeignKey(x => x.MaTaiKhoanThuNgan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
