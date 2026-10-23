using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemChiTietSaoLuu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DaKiemTraHopLe",
                table: "LichSuSaoLuus",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "KichThuocBytes",
                table: "LichSuSaoLuus",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyDo",
                table: "LichSuSaoLuus",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaBanNguon",
                table: "LichSuSaoLuus",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguonKichHoat",
                table: "LichSuSaoLuus",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenCSDL",
                table: "LichSuSaoLuus",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThoiLuongMs",
                table: "LichSuSaoLuus",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThongBaoLoi",
                table: "LichSuSaoLuus",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DaKiemTraHopLe",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "KichThuocBytes",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "LyDo",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "MaBanNguon",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "NguonKichHoat",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "TenCSDL",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "ThoiLuongMs",
                table: "LichSuSaoLuus");

            migrationBuilder.DropColumn(
                name: "ThongBaoLoi",
                table: "LichSuSaoLuus");
        }
    }
}
