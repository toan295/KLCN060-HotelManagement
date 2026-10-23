using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class GoiDichVuConfiguration : IEntityTypeConfiguration<GoiDichVu>
{
    public void Configure(EntityTypeBuilder<GoiDichVu> entity)
    {
        entity.HasKey(x => x.MaGoi);
        entity.Property(x => x.MaGoi).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.TenGoi).HasMaxLength(100).IsRequired();
        entity.Property(x => x.MoTa).HasMaxLength(255);
        entity.Property(x => x.GiaGoi).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.HoatDong).IsRequired().HasDefaultValue(true);
        entity.Property(x => x.NgayTao).IsRequired();
        entity.Property(x => x.TaoBoi).HasMaxLength(50).IsUnicode(false);

        entity.HasIndex(x => x.TenGoi).IsUnique();
    }
}

public class ChiTietGoiDichVuConfiguration : IEntityTypeConfiguration<ChiTietGoiDichVu>
{
    public void Configure(EntityTypeBuilder<ChiTietGoiDichVu> entity)
    {
        entity.HasKey(x => new { x.MaGoi, x.MaDV });
        entity.Property(x => x.MaGoi).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.MaDV).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.SoLuong).IsRequired().HasDefaultValue(1);

        // Xóa gói thì xóa luôn danh sách dịch vụ của gói; dịch vụ đang nằm trong một gói thì không xóa được.
        entity.HasOne(x => x.Goi).WithMany(x => x.ChiTietGois).HasForeignKey(x => x.MaGoi).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.DichVu).WithMany(x => x.ChiTietGois).HasForeignKey(x => x.MaDV).OnDelete(DeleteBehavior.Restrict);
    }
}
