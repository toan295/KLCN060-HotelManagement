using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class YeuCauDatLaiMatKhauConfiguration : IEntityTypeConfiguration<YeuCauDatLaiMatKhau>
{
    public void Configure(EntityTypeBuilder<YeuCauDatLaiMatKhau> entity)
    {
        entity.HasKey(x => x.MaYeuCau);
        entity.Property(x => x.MaYeuCau).ValueGeneratedOnAdd();

        entity.Property(x => x.TenDN).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        entity.Property(x => x.NgayTao).IsRequired();
        entity.Property(x => x.HanDung).IsRequired();
        entity.Property(x => x.DiaChiIP).HasMaxLength(45).IsUnicode(false);
        entity.Property(x => x.MaXacNhanHash).HasMaxLength(64).IsUnicode(false);
        entity.Property(x => x.SoLanNhapMa).HasDefaultValue(0);

        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasIndex(x => new { x.TenDN, x.NgayTao });

        entity.HasOne(x => x.TaiKhoan)
            .WithMany()
            .HasForeignKey(x => x.TenDN)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
