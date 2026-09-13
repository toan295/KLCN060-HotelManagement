using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class VaiTroQuyenConfiguration : IEntityTypeConfiguration<VaiTroQuyen>
{
    public void Configure(EntityTypeBuilder<VaiTroQuyen> entity)
    {
        entity.ToTable("VaiTro_Quyen");
        entity.HasKey(x => new { x.MaVaiTro, x.MaQuyen });

        entity.HasOne(x => x.VaiTro)
            .WithMany(x => x.VaiTroQuyens)
            .HasForeignKey(x => x.MaVaiTro)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.Quyen)
            .WithMany(x => x.VaiTroQuyens)
            .HasForeignKey(x => x.MaQuyen)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
