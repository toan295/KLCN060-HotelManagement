using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class CanhBaoTheoDoiConfiguration : IEntityTypeConfiguration<CanhBaoTheoDoi>
{
    public void Configure(EntityTypeBuilder<CanhBaoTheoDoi> entity)
    {
        entity.HasKey(x => x.MaTheoDoi);
        entity.Property(x => x.Ma).HasMaxLength(60).IsUnicode(false).IsRequired();
        entity.Property(x => x.TieuDe).HasMaxLength(200).IsRequired();
        entity.Property(x => x.MucDo).HasMaxLength(20).IsUnicode(false).IsRequired();
        entity.Property(x => x.NguoiNhan).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.NguoiGiao).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.GhiChuNhan).HasMaxLength(500);

        // Mỗi mã cảnh báo chỉ có MỘT đợt đang mở tại một thời điểm.
        entity.HasIndex(x => x.Ma).IsUnique().HasFilter("[DaXuLyLuc] IS NULL").HasDatabaseName("UX_CanhBaoTheoDoi_DangMo");
        entity.HasIndex(x => new { x.DaXuLyLuc, x.PhatHienLuc }).HasDatabaseName("IX_CanhBaoTheoDoi_DaXuLy");
    }
}
