using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class CoSoVatChatConfiguration : IEntityTypeConfiguration<CoSoVatChat>
{
    public void Configure(EntityTypeBuilder<CoSoVatChat> entity)
    {
        entity.HasKey(x => x.MaSo);
        entity.Property(x => x.MaSo).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.Ten).HasMaxLength(100).IsRequired();
        entity.Property(x => x.SoLuong).IsRequired().HasDefaultValue(1);
        entity.Property(x => x.TinhTrang).HasMaxLength(30).IsRequired();
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);

        entity.HasOne(x => x.Phong)
            .WithMany(x => x.CoSoVatChats)
            .HasForeignKey(x => x.MaPhong)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
