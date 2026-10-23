using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class DichVuConfiguration : IEntityTypeConfiguration<DichVu>
{
    public void Configure(EntityTypeBuilder<DichVu> entity)
    {
        entity.HasKey(x => x.MaDV);
        entity.Property(x => x.MaDV).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TenDV).HasMaxLength(100).IsRequired();
        entity.HasIndex(x => x.TenDV).IsUnique();
        entity.Property(x => x.GiaDV).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.DonViTinh).HasMaxLength(20).IsRequired();
        entity.Property(x => x.HoatDong).IsRequired().HasDefaultValue(true);
        entity.Property(x => x.LoaiDoanhThu).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(Domain.Enums.LoaiDoanhThuDichVu.DICH_VU);
    }
}
