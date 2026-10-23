using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GoQuyenChiPhiKhoiQuanLy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chi phi van hanh do Ke toan ghi nhan: go quyen QUAN_LY_CHI_PHI khoi vai tro QUAN_LY (cac vai tro khac giu nguyen).
            migrationBuilder.Sql("DELETE vq FROM VaiTro_Quyen vq JOIN VaiTros v ON v.MaVaiTro = vq.MaVaiTro JOIN Quyens q ON q.MaQuyen = vq.MaQuyen WHERE v.TenVaiTro = N'QUAN_LY' AND q.TenQuyen = N'QUAN_LY_CHI_PHI'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO VaiTro_Quyen (MaVaiTro, MaQuyen) SELECT v.MaVaiTro, q.MaQuyen FROM VaiTros v CROSS JOIN Quyens q WHERE v.TenVaiTro = N'QUAN_LY' AND q.TenQuyen = N'QUAN_LY_CHI_PHI' AND NOT EXISTS (SELECT 1 FROM VaiTro_Quyen x WHERE x.MaVaiTro = v.MaVaiTro AND x.MaQuyen = q.MaQuyen)");
        }
    }
}
