using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class PhieuDoiPhongConfiguration : IEntityTypeConfiguration<PhieuDoiPhong>
{
    public void Configure(EntityTypeBuilder<PhieuDoiPhong> entity)
    {
        entity.HasKey(x => x.MaPhieuDoi);
        entity.Property(x => x.MaPhieuDoi).HasMaxLength(15).IsUnicode(false);

        entity.Property(x => x.MaPhieuNhan).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaPhongCu).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaPhongMoi).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.NgayDoi).IsRequired();
        entity.Property(x => x.ChenhLechGia).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.GhiChu).HasMaxLength(255);

        // FK kép (MaPhieuNhan, MaPhongCu) trỏ về khóa chính kép của ChiTietPhieuNhan.
        entity.HasOne(x => x.ChiTietPhieuNhanCu)
            .WithMany()
            .HasForeignKey(x => new { x.MaPhieuNhan, x.MaPhongCu })
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.PhongMoi)
            .WithMany(x => x.PhieuDoiPhongMois)
            .HasForeignKey(x => x.MaPhongMoi)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
