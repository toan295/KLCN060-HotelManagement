using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class LichSuTinhTrangPhongConfiguration : IEntityTypeConfiguration<LichSuTinhTrangPhong>
{
    public void Configure(EntityTypeBuilder<LichSuTinhTrangPhong> entity)
    {
        entity.HasKey(x => x.MaLichSu);
        entity.Property(x => x.MaLichSu).ValueGeneratedOnAdd();

        entity.Property(x => x.MaPhong).HasMaxLength(10).IsUnicode(false).IsRequired();
        entity.Property(x => x.TuTinhTrang).HasConversion<string>().HasMaxLength(5).IsUnicode(false).IsRequired();
        entity.Property(x => x.DenTinhTrang).HasConversion<string>().HasMaxLength(5).IsUnicode(false).IsRequired();
        entity.Property(x => x.ThoiGian).IsRequired();
        entity.Property(x => x.MaTaiKhoan).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.LyDo).HasMaxLength(255);

        // Lịch sử là dữ liệu truy vết: xóa một phòng đã có lịch sử phải bị chặn (Restrict) chứ không được kéo theo xóa lịch sử.
        entity.HasOne(x => x.Phong)
            .WithMany(x => x.LichSuTinhTrangs)
            .HasForeignKey(x => x.MaPhong)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.MaPhong, x.ThoiGian });
        entity.HasIndex(x => x.ThoiGian);
    }
}
