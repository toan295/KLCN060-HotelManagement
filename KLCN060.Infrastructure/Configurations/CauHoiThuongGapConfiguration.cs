using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KLCN060.Infrastructure.Configurations;

public class CauHoiThuongGapConfiguration : IEntityTypeConfiguration<CauHoiThuongGap>
{
    public void Configure(EntityTypeBuilder<CauHoiThuongGap> entity)
    {
        entity.HasKey(x => x.MaCauHoi);
        entity.Property(x => x.MaCauHoi).ValueGeneratedOnAdd();

        entity.Property(x => x.TuKhoa).HasMaxLength(255).IsRequired();
        entity.Property(x => x.CauTraLoi).HasMaxLength(1000).IsRequired();
    }
}
