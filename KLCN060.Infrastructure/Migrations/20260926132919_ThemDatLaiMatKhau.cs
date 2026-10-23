using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemDatLaiMatKhau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YeuCauDatLaiMatKhaus",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenDN = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanDung = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgaySuDung = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiaChiIP = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauDatLaiMatKhaus", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauDatLaiMatKhaus_TaiKhoans_TenDN",
                        column: x => x.TenDN,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDatLaiMatKhaus_TenDN_NgayTao",
                table: "YeuCauDatLaiMatKhaus",
                columns: new[] { "TenDN", "NgayTao" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDatLaiMatKhaus_TokenHash",
                table: "YeuCauDatLaiMatKhaus",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauDatLaiMatKhaus");
        }
    }
}
