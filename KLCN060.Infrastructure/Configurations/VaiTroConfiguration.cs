using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class VaiTroConfiguration : IEntityTypeConfiguration<VaiTro>
{
    public void Configure(EntityTypeBuilder<VaiTro> entity)
    {
        entity.HasKey(x => x.MaVaiTro);

        entity.Property(x => x.TenVaiTro)
            .HasMaxLength(50)
            .IsRequired();
        entity.HasIndex(x => x.TenVaiTro).IsUnique();

        entity.Property(x => x.MoTa)
            .HasMaxLength(255);
    }
}
