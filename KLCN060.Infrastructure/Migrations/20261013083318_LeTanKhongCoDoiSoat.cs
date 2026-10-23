using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeTanKhongCoDoiSoat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đối soát chuyển khoản là việc của Kế toán/Quản lý: gỡ quyền XEM_DOI_SOAT khỏi vai trò LE_TAN
            // (lễ tân vẫn tạo mã QR và xác nhận chuyển khoản ở màn Thanh toán bằng quyền THANH_TOAN).
            migrationBuilder.Sql(@"
DELETE vq
FROM VaiTro_Quyen vq
JOIN VaiTros v ON v.MaVaiTro = vq.MaVaiTro
JOIN Quyens q ON q.MaQuyen = vq.MaQuyen
WHERE v.TenVaiTro = 'LE_TAN' AND q.TenQuyen = 'XEM_DOI_SOAT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO VaiTro_Quyen (MaVaiTro, MaQuyen)
SELECT v.MaVaiTro, q.MaQuyen
FROM VaiTros v CROSS JOIN Quyens q
WHERE v.TenVaiTro = 'LE_TAN' AND q.TenQuyen = 'XEM_DOI_SOAT'
  AND NOT EXISTS (SELECT 1 FROM VaiTro_Quyen x WHERE x.MaVaiTro = v.MaVaiTro AND x.MaQuyen = q.MaQuyen);");
        }
    }
}
