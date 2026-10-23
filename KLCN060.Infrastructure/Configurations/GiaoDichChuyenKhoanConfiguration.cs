using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class GiaoDichChuyenKhoanConfiguration : IEntityTypeConfiguration<GiaoDichChuyenKhoan>
{
    public void Configure(EntityTypeBuilder<GiaoDichChuyenKhoan> entity)
    {
        entity.HasKey(x => x.MaGiaoDich);
        entity.Property(x => x.MaGiaoDich).HasMaxLength(15).IsUnicode(false);

        entity.Property(x => x.LoaiThamChieu).HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.MaThamChieu).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.SoTien).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.NoiDungChuyenKhoan).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(x => x.NgayTao).IsRequired();
        entity.Property(x => x.HanThanhToan).IsRequired();
        entity.Property(x => x.MaGiaoDichNganHang).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.MaTaiKhoanTao).HasMaxLength(50).IsUnicode(false).IsRequired();
        entity.Property(x => x.MaNVThuNgan).HasMaxLength(10).IsUnicode(false);

        // Nội dung chuyển khoản là khóa đối soát nên không được trùng giữa 2 giao dịch.
        entity.HasIndex(x => x.NoiDungChuyenKhoan).IsUnique();
        entity.HasIndex(x => new { x.LoaiThamChieu, x.MaThamChieu, x.TrangThai });

        entity.HasOne(x => x.TaiKhoanTao)
            .WithMany()
            .HasForeignKey(x => x.MaTaiKhoanTao)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.NhanVienThuNgan)
            .WithMany()
            .HasForeignKey(x => x.MaNVThuNgan)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
