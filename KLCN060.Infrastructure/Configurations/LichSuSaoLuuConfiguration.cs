using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class LichSuSaoLuuConfiguration : IEntityTypeConfiguration<LichSuSaoLuu>
{
    public void Configure(EntityTypeBuilder<LichSuSaoLuu> entity)
    {
        entity.HasKey(x => x.MaLichSu);
        entity.Property(x => x.MaLichSu).ValueGeneratedOnAdd();

        entity.Property(x => x.LoaiThaoTac)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.ThoiGianThucHien).IsRequired().HasDefaultValueSql("GETDATE()");
        entity.Property(x => x.MaTaiKhoan).HasMaxLength(50).IsUnicode(false).IsRequired();
        // Unicode: người dùng có thể chọn thư mục sao lưu có tên tiếng Việt (VD thư mục "Sao lưu KS").
        entity.Property(x => x.DuongDanFile).HasMaxLength(500);

        entity.Property(x => x.KetQua)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(x => x.GhiChu).HasMaxLength(255);
        entity.Property(x => x.TenCSDL).HasMaxLength(128);
        entity.Property(x => x.NguonKichHoat).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.LyDo).HasMaxLength(255);
        entity.Property(x => x.ThongBaoLoi).HasMaxLength(1000);

        entity.HasOne(x => x.TaiKhoan)
            .WithMany(x => x.LichSuSaoLuus)
            .HasForeignKey(x => x.MaTaiKhoan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
