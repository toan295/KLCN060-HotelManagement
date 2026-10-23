using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemDieuKienKhuyenMai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LyDoTamNgung",
                table: "KhuyenMais",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaLoaiApDung",
                table: "KhuyenMais",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoDemToiThieu",
                table: "KhuyenMais",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TamNgung",
                table: "KhuyenMais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TongTienToiThieu",
                table: "KhuyenMais",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LyDoTamNgung",
                table: "KhuyenMais");

            migrationBuilder.DropColumn(
                name: "MaLoaiApDung",
                table: "KhuyenMais");

            migrationBuilder.DropColumn(
                name: "SoDemToiThieu",
                table: "KhuyenMais");

            migrationBuilder.DropColumn(
                name: "TamNgung",
                table: "KhuyenMais");

            migrationBuilder.DropColumn(
                name: "TongTienToiThieu",
                table: "KhuyenMais");
        }
    }
}
