using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class ThongBaoNganHangConfiguration : IEntityTypeConfiguration<ThongBaoNganHang>
{
    public void Configure(EntityTypeBuilder<ThongBaoNganHang> entity)
    {
        entity.HasKey(x => x.MaThongBao);
        entity.Property(x => x.MaThongBao).ValueGeneratedOnAdd();

        entity.Property(x => x.MaGiaoDichNganHang).HasMaxLength(100).IsUnicode(false).IsRequired();
        entity.Property(x => x.SoTaiKhoanNhan).HasMaxLength(30).IsUnicode(false).IsRequired();
        entity.Property(x => x.SoTien).HasColumnType("decimal(18,2)").IsRequired();
        entity.Property(x => x.NoiDung).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ThoiGianNganHang).IsRequired();
        entity.Property(x => x.ThoiGianNhan).IsRequired();
        entity.Property(x => x.KetQua).HasConversion<string>().HasMaxLength(40).IsRequired();
        entity.Property(x => x.MaGiaoDich).HasMaxLength(15).IsUnicode(false);
        entity.Property(x => x.GhiChu).HasMaxLength(255);
        entity.Property(x => x.TrangThaiXuLy).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(Domain.Enums.TrangThaiXuLyDoiSoat.KHONG_CAN);
        entity.Property(x => x.NguoiXuLy).HasMaxLength(50).IsUnicode(false);
        entity.Property(x => x.HuongXuLy).HasMaxLength(20).IsUnicode(false);
        entity.Property(x => x.GhiChuXuLy).HasMaxLength(500);
        entity.HasIndex(x => x.TrangThaiXuLy);

        // Chống xử lý trùng: mỗi giao dịch ngân hàng chỉ được ghi nhận một lần.
        entity.HasIndex(x => x.MaGiaoDichNganHang).IsUnique();

        entity.HasOne(x => x.GiaoDich)
            .WithMany(x => x.ThongBaoNganHangs)
            .HasForeignKey(x => x.MaGiaoDich)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
