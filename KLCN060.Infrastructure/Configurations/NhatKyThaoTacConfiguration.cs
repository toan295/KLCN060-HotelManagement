using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class NhatKyThaoTacConfiguration : IEntityTypeConfiguration<NhatKyThaoTac>
{
    public void Configure(EntityTypeBuilder<NhatKyThaoTac> entity)
    {
        entity.HasKey(x => x.MaNhatKy);
        entity.Property(x => x.MaNhatKy).ValueGeneratedOnAdd();

        entity.Property(x => x.MaTaiKhoan).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.HanhDong).HasMaxLength(100).IsUnicode(false).IsRequired();
        entity.Property(x => x.DoiTuongTacDong).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.ThoiGian).IsRequired().HasDefaultValueSql("GETDATE()");
        entity.Property(x => x.DiaChiIP).HasMaxLength(45).IsUnicode(false);
        entity.Property(x => x.ChiTiet).HasColumnType("nvarchar(max)");

        entity.HasOne(x => x.TaiKhoan)
            .WithMany(x => x.NhatKyThaoTacs)
            .HasForeignKey(x => x.MaTaiKhoan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
