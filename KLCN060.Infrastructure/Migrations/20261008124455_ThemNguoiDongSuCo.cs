using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemNguoiDongSuCo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NguoiDong",
                table: "SuCoPhongs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            // Sự cố đã đóng trước khi có cột này: người xử lý gần nhất được coi là người đóng (không có dữ liệu chính xác hơn).
            migrationBuilder.Sql("UPDATE SuCoPhongs SET NguoiDong = NguoiXuLy WHERE TrangThai IN ('DA_XU_LY', 'DA_HUY') AND NguoiXuLy IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NguoiDong",
                table: "SuCoPhongs");
        }
    }
}
