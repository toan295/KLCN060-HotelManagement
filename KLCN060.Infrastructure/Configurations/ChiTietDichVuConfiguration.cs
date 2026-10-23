using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ChiTietDichVuConfiguration : IEntityTypeConfiguration<ChiTietDichVu>
{
    public void Configure(EntityTypeBuilder<ChiTietDichVu> entity)
    {
        entity.HasKey(x => x.MaCTDV);
        entity.Property(x => x.MaCTDV).HasMaxLength(10).IsUnicode(false);

        entity.Property(x => x.MaDV).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.TieuDe).HasMaxLength(100).IsRequired();
        entity.Property(x => x.NoiDung).HasMaxLength(500).IsRequired();
        entity.Property(x => x.ThuTu).IsRequired().HasDefaultValue(0);
        entity.Property(x => x.ConHienThi).IsRequired().HasDefaultValue(true);

        // Một dịch vụ không có 2 mục cùng tiêu đề.
        entity.HasIndex(x => new { x.MaDV, x.TieuDe }).IsUnique();

        // Xóa dịch vụ thì xóa luôn phần mô tả chi tiết của nó (việc xóa vẫn bị chặn nếu dịch vụ đã có lượt sử dụng).
        entity.HasOne(x => x.DichVu)
            .WithMany(x => x.ChiTietDichVus)
            .HasForeignKey(x => x.MaDV)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
