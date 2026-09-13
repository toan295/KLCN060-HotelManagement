using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class LoaiPhongConfiguration : IEntityTypeConfiguration<LoaiPhong>
{
    public void Configure(EntityTypeBuilder<LoaiPhong> entity)
    {
        entity.HasKey(x => x.MaLoai);
        entity.Property(x => x.MaLoai).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TenLoai).HasMaxLength(50).IsRequired();
        entity.Property(x => x.SoNguoiTieuChuan).IsRequired();
        entity.Property(x => x.DonGia).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.PhuThu).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
    }
}
