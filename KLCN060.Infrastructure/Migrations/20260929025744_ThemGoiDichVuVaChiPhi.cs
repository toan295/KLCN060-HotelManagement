using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemGoiDichVuVaChiPhi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HoatDong",
                table: "DichVus",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "MaGoi",
                table: "ChiTietSuDungDVs",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChiPhis",
                columns: table => new
                {
                    MaChiPhi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NgayChiPhi = table.Column<DateOnly>(type: "date", nullable: false),
                    Loai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NhaCungCap = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GhiBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuaBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NgaySua = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiPhis", x => x.MaChiPhi);
                });

            migrationBuilder.CreateTable(
                name: "GoiDichVus",
                columns: table => new
                {
                    MaGoi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenGoi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GiaGoi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaoBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoiDichVus", x => x.MaGoi);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietGoiDichVus",
                columns: table => new
                {
                    MaGoi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietGoiDichVus", x => new { x.MaGoi, x.MaDV });
                    table.ForeignKey(
                        name: "FK_ChiTietGoiDichVus_DichVus_MaDV",
                        column: x => x.MaDV,
                        principalTable: "DichVus",
                        principalColumn: "MaDV",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietGoiDichVus_GoiDichVus_MaGoi",
                        column: x => x.MaGoi,
                        principalTable: "GoiDichVus",
                        principalColumn: "MaGoi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietSuDungDVs_MaGoi",
                table: "ChiTietSuDungDVs",
                column: "MaGoi");

            migrationBuilder.CreateIndex(
                name: "IX_ChiPhis_NgayChiPhi",
                table: "ChiPhis",
                column: "NgayChiPhi");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietGoiDichVus_MaDV",
                table: "ChiTietGoiDichVus",
                column: "MaDV");

            migrationBuilder.CreateIndex(
                name: "IX_GoiDichVus_TenGoi",
                table: "GoiDichVus",
                column: "TenGoi",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietSuDungDVs_GoiDichVus_MaGoi",
                table: "ChiTietSuDungDVs",
                column: "MaGoi",
                principalTable: "GoiDichVus",
                principalColumn: "MaGoi",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChiTietSuDungDVs_GoiDichVus_MaGoi",
                table: "ChiTietSuDungDVs");

            migrationBuilder.DropTable(
                name: "ChiPhis");

            migrationBuilder.DropTable(
                name: "ChiTietGoiDichVus");

            migrationBuilder.DropTable(
                name: "GoiDichVus");

            migrationBuilder.DropIndex(
                name: "IX_ChiTietSuDungDVs_MaGoi",
                table: "ChiTietSuDungDVs");

            migrationBuilder.DropColumn(
                name: "HoatDong",
                table: "DichVus");

            migrationBuilder.DropColumn(
                name: "MaGoi",
                table: "ChiTietSuDungDVs");
        }
    }
}
