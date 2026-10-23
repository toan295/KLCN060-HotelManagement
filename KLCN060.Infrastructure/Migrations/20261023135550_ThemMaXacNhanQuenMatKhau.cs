using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemMaXacNhanQuenMatKhau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaXacNhanHash",
                table: "YeuCauDatLaiMatKhaus",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoLanNhapMa",
                table: "YeuCauDatLaiMatKhaus",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaXacNhanHash",
                table: "YeuCauDatLaiMatKhaus");

            migrationBuilder.DropColumn(
                name: "SoLanNhapMa",
                table: "YeuCauDatLaiMatKhaus");
        }
    }
}
