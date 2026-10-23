using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemMoTaLoaiPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DienTich",
                table: "LoaiPhongs",
                type: "decimal(6,1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoaiGiuong",
                table: "LoaiPhongs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoTa",
                table: "LoaiPhongs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TienNghi",
                table: "LoaiPhongs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DienTich",
                table: "LoaiPhongs");

            migrationBuilder.DropColumn(
                name: "LoaiGiuong",
                table: "LoaiPhongs");

            migrationBuilder.DropColumn(
                name: "MoTa",
                table: "LoaiPhongs");

            migrationBuilder.DropColumn(
                name: "TienNghi",
                table: "LoaiPhongs");
        }
    }
}
