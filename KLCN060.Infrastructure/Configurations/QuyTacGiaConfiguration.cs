using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class QuyTacGiaConfiguration : IEntityTypeConfiguration<QuyTacGia>
{
    public void Configure(EntityTypeBuilder<QuyTacGia> entity)
    {
        entity.HasKey(x => x.MaQuyTac);
        entity.Property(x => x.MaQuyTac).ValueGeneratedOnAdd();

        entity.Property(x => x.TenQuyTac).HasMaxLength(100).IsRequired();
        entity.Property(x => x.LoaiQuyTac).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaLoai).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.KieuDieuChinh).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.GiaTri).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.TuNgay).HasColumnType("date");
        entity.Property(x => x.DenNgay).HasColumnType("date");
        entity.Property(x => x.CacThuApDung).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.LoaiDatPhongApDung).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.UuTien).IsRequired().HasDefaultValue(0);
        entity.Property(x => x.HoatDong).IsRequired().HasDefaultValue(true);
        entity.Property(x => x.GhiChu).HasMaxLength(255);
        entity.Property(x => x.NgayTao).IsRequired();
        entity.Property(x => x.TaoBoi).HasMaxLength(50).IsUnicode(false);

        entity.HasOne(x => x.LoaiPhong).WithMany().HasForeignKey(x => x.MaLoai).OnDelete(DeleteBehavior.Cascade);
    }
}
