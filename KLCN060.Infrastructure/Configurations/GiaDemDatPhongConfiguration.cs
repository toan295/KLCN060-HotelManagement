using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class GiaDemDatPhongConfiguration : IEntityTypeConfiguration<GiaDemDatPhong>
{
    public void Configure(EntityTypeBuilder<GiaDemDatPhong> entity)
    {
        entity.HasKey(x => new { x.MaPhieuDat, x.MaPhong, x.Dem });
        entity.Property(x => x.MaPhieuDat).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false);
        entity.Property(x => x.GiaCoBan).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.Gia).HasColumnType("decimal(18,2)").IsRequired();

        // Xóa dòng phòng của phiếu (đổi phòng) thì bỏ luôn giá từng đêm của dòng đó.
        entity.HasOne(x => x.ChiTietPhieuDat)
            .WithMany(x => x.GiaTheoDems)
            .HasForeignKey(x => new { x.MaPhieuDat, x.MaPhong })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
