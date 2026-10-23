using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemGiaChotTungDem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GiaDemDatPhongs",
                columns: table => new
                {
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Dem = table.Column<DateOnly>(type: "date", nullable: false),
                    GiaCoBan = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Gia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaQuyTac = table.Column<int>(type: "int", nullable: true),
                    TenQuyTac = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DieuChinh = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaDemDatPhongs", x => new { x.MaPhieuDat, x.MaPhong, x.Dem });
                    table.ForeignKey(
                        name: "FK_GiaDemDatPhongs_ChiTietPhieuDats_MaPhieuDat_MaPhong",
                        columns: x => new { x.MaPhieuDat, x.MaPhong },
                        principalTable: "ChiTietPhieuDats",
                        principalColumns: new[] { "MaPhieuDat", "MaPhong" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiaDemDatPhongs");
        }
    }
}
