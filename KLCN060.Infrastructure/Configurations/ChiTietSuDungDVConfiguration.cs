using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietSuDungDVConfiguration : IEntityTypeConfiguration<ChiTietSuDungDV>
{
    public void Configure(EntityTypeBuilder<ChiTietSuDungDV> entity)
    {
        entity.HasKey(x => x.MaChiTietDV);
        entity.Property(x => x.MaChiTietDV).ValueGeneratedOnAdd();

        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaDV).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.SoLuong).IsRequired().HasDefaultValue(1);
        entity.Property(x => x.NgaySuDung).IsRequired();
        entity.Property(x => x.ThanhTien).HasColumnType("decimal(18,2)").IsRequired();

        entity.HasOne(x => x.PhieuNhanPhong)
            .WithMany(x => x.ChiTietSuDungDVs)
            .HasForeignKey(x => x.MaPhieuNhan)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.DichVu)
            .WithMany(x => x.ChiTietSuDungDVs)
            .HasForeignKey(x => x.MaDV)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
