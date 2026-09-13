using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietHoaDonConfiguration : IEntityTypeConfiguration<ChiTietHoaDon>
{
    public void Configure(EntityTypeBuilder<ChiTietHoaDon> entity)
    {
        entity.HasKey(x => x.MaChiTietHoaDon);
        entity.Property(x => x.MaChiTietHoaDon).ValueGeneratedOnAdd();

        entity.Property(x => x.MaHD).HasMaxLength(15).IsUnicode(false).IsRequired();

        entity.Property(x => x.LoaiKhoanMuc)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.MoTa).HasMaxLength(255).IsRequired();
        entity.Property(x => x.SoLuong).IsRequired().HasDefaultValue(1);
        entity.Property(x => x.DonGia).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.ThanhTien).HasColumnType("decimal(18,2)").IsRequired();

        entity.HasOne(x => x.HoaDon)
            .WithMany(x => x.ChiTietHoaDons)
            .HasForeignKey(x => x.MaHD)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
