using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class HoaDonDienTuConfiguration : IEntityTypeConfiguration<HoaDonDienTu>
{
    public void Configure(EntityTypeBuilder<HoaDonDienTu> entity)
    {
        entity.HasKey(x => x.MaHDDT);
        entity.Property(x => x.MaHDDT).ValueGeneratedOnAdd();

        entity.Property(x => x.MaHD).HasMaxLength(15).IsUnicode(false).IsRequired();
        entity.Property(x => x.TrangThai).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();

        entity.Property(x => x.TenNguoiMua).HasMaxLength(200).IsRequired();
        entity.Property(x => x.TenDonVi).HasMaxLength(300);
        entity.Property(x => x.MaSoThue).HasMaxLength(14).IsUnicode(false);
        entity.Property(x => x.DiaChi).HasMaxLength(300);
        entity.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);

        entity.Property(x => x.ThueSuat).HasColumnType("decimal(5,2)");
        entity.Property(x => x.TienChuaThue).HasColumnType("decimal(18,2)");
        entity.Property(x => x.TienThue).HasColumnType("decimal(18,2)");
        entity.Property(x => x.TongThanhToan).HasColumnType("decimal(18,2)");

        entity.Property(x => x.NhaCungCap).HasMaxLength(30).IsUnicode(false);
        entity.Property(x => x.MauSo).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.KyHieu).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.SoHoaDon).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.MaCQT).HasMaxLength(60).IsUnicode(false);
        entity.Property(x => x.MaTraCuu).HasMaxLength(60).IsUnicode(false);
        entity.Property(x => x.LienKetTraCuu).HasMaxLength(500).IsUnicode(false);
        entity.Property(x => x.MaThamChieuNhaCungCap).HasMaxLength(100).IsUnicode(false);
        entity.Property(x => x.ThongBaoLoi).HasMaxLength(1000);
        entity.Property(x => x.NguoiYeuCau).HasMaxLength(50).IsUnicode(false).IsRequired();

        // Mỗi hóa đơn chỉ có đúng một HĐĐT: chặn phát hành hai lần.
        entity.HasIndex(x => x.MaHD).IsUnique();

        entity.HasOne(x => x.HoaDon).WithMany().HasForeignKey(x => x.MaHD).OnDelete(DeleteBehavior.Restrict);
    }
}
