using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class KhuyenMaiConfiguration : IEntityTypeConfiguration<KhuyenMai>
{
    public void Configure(EntityTypeBuilder<KhuyenMai> entity)
    {
        entity.HasKey(x => x.MaKM);
        entity.Property(x => x.MaKM).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TenKM).HasMaxLength(100).IsRequired();
        entity.Property(x => x.PhanTramKM).HasColumnType("decimal(5,2)");
        entity.Property(x => x.NgayBatDau).HasColumnType("date").IsRequired();
        entity.Property(x => x.NgayKetThuc).HasColumnType("date").IsRequired();
        entity.Property(x => x.DieuKien).HasMaxLength(255);

        entity.Property(x => x.LoaiKM)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.GiaTri).HasColumnType("decimal(18,2)");
        entity.Property(x => x.SoLuongGioiHan);
        entity.Property(x => x.SoLuongDaSuDung).IsRequired().HasDefaultValue(0);
    }
}
