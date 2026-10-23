using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoNhatKyBanGiaoCa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhatKyBanGiaoCas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NhatKyBanGiaoCas",
                columns: table => new
                {
                    MaBanGiao = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoanGiao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaTaiKhoanNhan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SoLuongPhieuTrongCa = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ThoiGianBanGiao = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    TongTienMatCuoiCa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TongTienMatDauCa = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyBanGiaoCas", x => x.MaBanGiao);
                    table.ForeignKey(
                        name: "FK_NhatKyBanGiaoCas_TaiKhoans_MaTaiKhoanGiao",
                        column: x => x.MaTaiKhoanGiao,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NhatKyBanGiaoCas_TaiKhoans_MaTaiKhoanNhan",
                        column: x => x.MaTaiKhoanNhan,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyBanGiaoCas_MaTaiKhoanGiao",
                table: "NhatKyBanGiaoCas",
                column: "MaTaiKhoanGiao");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyBanGiaoCas_MaTaiKhoanNhan",
                table: "NhatKyBanGiaoCas",
                column: "MaTaiKhoanNhan");
        }
    }
}
