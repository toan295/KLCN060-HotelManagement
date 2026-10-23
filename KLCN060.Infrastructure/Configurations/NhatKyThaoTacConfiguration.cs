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
        // HanhDong/DoiTuongTacDong là nvarchar: đối tượng có thể chứa tiếng Việt (VD vị trí "Sảnh chính" của đồ bỏ quên);
        // varchar làm mất dấu thành "?" nên chữ ký HMAC của dòng nhật ký không còn khớp.
        entity.Property(x => x.HanhDong).HasMaxLength(100).IsRequired();
        entity.Property(x => x.DoiTuongTacDong).HasMaxLength(100);
        entity.Property(x => x.ThoiGian).IsRequired().HasDefaultValueSql("GETDATE()");
        entity.Property(x => x.DiaChiIP).HasMaxLength(45).IsUnicode(false);
        entity.Property(x => x.ChiTiet).HasColumnType("nvarchar(max)");
        entity.Property(x => x.KetQua).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue("THANH_CONG");
        entity.Property(x => x.MaLoi).HasMaxLength(80).IsUnicode(false);
        entity.Property(x => x.ChuKy).HasMaxLength(64).IsUnicode(false);

        // Nhật ký chỉ được thêm mới: trigger chặn UPDATE/DELETE (xem migration BaoVeNhatKyHoatDong); khai báo để EF không dùng OUTPUT trực tiếp.
        entity.ToTable(t => t.HasTrigger("TR_NhatKyThaoTac_ChiThem"));

        // Truy vấn nhật ký theo thời gian, người, đối tượng, hành động, kết quả khi dữ liệu lớn.
        entity.HasIndex(x => new { x.ThoiGian, x.MaNhatKy }).IsDescending(true, true).HasDatabaseName("IX_NhatKyThaoTac_ThoiGian");
        entity.HasIndex(x => new { x.MaTaiKhoan, x.ThoiGian }).HasDatabaseName("IX_NhatKyThaoTac_TaiKhoan_ThoiGian");
        entity.HasIndex(x => x.DoiTuongTacDong).HasDatabaseName("IX_NhatKyThaoTac_DoiTuong");
        entity.HasIndex(x => x.HanhDong).HasDatabaseName("IX_NhatKyThaoTac_HanhDong");
        entity.HasIndex(x => new { x.KetQua, x.ThoiGian }).HasDatabaseName("IX_NhatKyThaoTac_KetQua_ThoiGian");

        entity.HasOne(x => x.TaiKhoan)
            .WithMany(x => x.NhatKyThaoTacs)
            .HasForeignKey(x => x.MaTaiKhoan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
