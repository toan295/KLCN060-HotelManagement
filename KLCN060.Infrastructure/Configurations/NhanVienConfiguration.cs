using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class NhanVienConfiguration : IEntityTypeConfiguration<NhanVien>
{
    public void Configure(EntityTypeBuilder<NhanVien> entity)
    {
        entity.HasKey(x => x.MaNV);
        entity.Property(x => x.MaNV).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.HoTen).HasMaxLength(100).IsRequired();
        entity.Property(x => x.SoDT).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.ChucVu).HasMaxLength(50).IsRequired();

        entity.HasOne(x => x.VaiTro)
            .WithMany(x => x.NhanViens)
            .HasForeignKey(x => x.MaVaiTro)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
