using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class QuyenConfiguration : IEntityTypeConfiguration<Quyen>
{
    public void Configure(EntityTypeBuilder<Quyen> entity)
    {
        entity.HasKey(x => x.MaQuyen);

        entity.Property(x => x.TenQuyen)
            .HasMaxLength(100)
            .IsRequired();
        entity.HasIndex(x => x.TenQuyen).IsUnique();

        entity.Property(x => x.NhomChucNang)
            .HasMaxLength(50);
    }
}
