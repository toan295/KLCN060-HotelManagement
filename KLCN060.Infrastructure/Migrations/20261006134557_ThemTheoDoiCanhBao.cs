using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemTheoDoiCanhBao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanhBaoTheoDois",
                columns: table => new
                {
                    MaTheoDoi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ma = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MucDo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SoLuongLucPhatHien = table.Column<int>(type: "int", nullable: false),
                    SoLuongHienTai = table.Column<int>(type: "int", nullable: false),
                    PhatHienLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaXuLyLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiNhan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NguoiGiao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NhanLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HanXuLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GhiChuNhan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanhBaoTheoDois", x => x.MaTheoDoi);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanhBaoTheoDoi_DaXuLy",
                table: "CanhBaoTheoDois",
                columns: new[] { "DaXuLyLuc", "PhatHienLuc" });

            migrationBuilder.CreateIndex(
                name: "UX_CanhBaoTheoDoi_DangMo",
                table: "CanhBaoTheoDois",
                column: "Ma",
                unique: true,
                filter: "[DaXuLyLuc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanhBaoTheoDois");
        }
    }
}
