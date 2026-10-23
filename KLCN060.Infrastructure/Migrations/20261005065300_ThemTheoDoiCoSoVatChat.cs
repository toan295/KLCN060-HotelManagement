using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemTheoDoiCoSoVatChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CapNhatLuc",
                table: "CoSoVatChats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhiChu",
                table: "CoSoVatChats",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyDoTinhTrang",
                table: "CoSoVatChats",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayTao",
                table: "CoSoVatChats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiCapNhat",
                table: "CoSoVatChats",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TinhTrangTuLuc",
                table: "CoSoVatChats",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapNhatLuc",
                table: "CoSoVatChats");

            migrationBuilder.DropColumn(
                name: "GhiChu",
                table: "CoSoVatChats");

            migrationBuilder.DropColumn(
                name: "LyDoTinhTrang",
                table: "CoSoVatChats");

            migrationBuilder.DropColumn(
                name: "NgayTao",
                table: "CoSoVatChats");

            migrationBuilder.DropColumn(
                name: "NguoiCapNhat",
                table: "CoSoVatChats");

            migrationBuilder.DropColumn(
                name: "TinhTrangTuLuc",
                table: "CoSoVatChats");
        }
    }
}
