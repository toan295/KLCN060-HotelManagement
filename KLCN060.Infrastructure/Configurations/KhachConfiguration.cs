using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class KhachConfiguration : IEntityTypeConfiguration<Khach>
{
    public void Configure(EntityTypeBuilder<Khach> entity)
    {
        entity.HasKey(x => x.MaKhach);
        entity.Property(x => x.MaKhach).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.HoTen).HasMaxLength(100).IsRequired();
        entity.Property(x => x.SoDT).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.CCCD).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.DiaChi).HasMaxLength(255);
    }
}
