using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoChiPhiVanHanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiPhis");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChiPhis",
                columns: table => new
                {
                    MaChiPhi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GhiBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Loai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NgayChiPhi = table.Column<DateOnly>(type: "date", nullable: false),
                    NgaySua = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NhaCungCap = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NoiDung = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SuaBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiPhis", x => x.MaChiPhi);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiPhis_NgayChiPhi",
                table: "ChiPhis",
                column: "NgayChiPhi");
        }
    }
}
