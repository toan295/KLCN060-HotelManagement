using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemThanhToanChuyenKhoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GiaoDichChuyenKhoans",
                columns: table => new
                {
                    MaGiaoDich = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    LoaiThamChieu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaThamChieu = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NoiDungChuyenKhoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanThanhToan = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayThanhToan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaGiaoDichNganHang = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    MaTaiKhoanTao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaNVThuNgan = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaoDichChuyenKhoans", x => x.MaGiaoDich);
                    table.ForeignKey(
                        name: "FK_GiaoDichChuyenKhoans_NhanViens_MaNVThuNgan",
                        column: x => x.MaNVThuNgan,
                        principalTable: "NhanViens",
                        principalColumn: "MaNV",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichChuyenKhoans_TaiKhoans_MaTaiKhoanTao",
                        column: x => x.MaTaiKhoanTao,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThongBaoNganHangs",
                columns: table => new
                {
                    MaThongBao = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaGiaoDichNganHang = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SoTaiKhoanNhan = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ThoiGianNganHang = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianNhan = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KetQua = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MaGiaoDich = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBaoNganHangs", x => x.MaThongBao);
                    table.ForeignKey(
                        name: "FK_ThongBaoNganHangs_GiaoDichChuyenKhoans_MaGiaoDich",
                        column: x => x.MaGiaoDich,
                        principalTable: "GiaoDichChuyenKhoans",
                        principalColumn: "MaGiaoDich",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichChuyenKhoans_LoaiThamChieu_MaThamChieu_TrangThai",
                table: "GiaoDichChuyenKhoans",
                columns: new[] { "LoaiThamChieu", "MaThamChieu", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichChuyenKhoans_MaNVThuNgan",
                table: "GiaoDichChuyenKhoans",
                column: "MaNVThuNgan");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichChuyenKhoans_MaTaiKhoanTao",
                table: "GiaoDichChuyenKhoans",
                column: "MaTaiKhoanTao");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichChuyenKhoans_NoiDungChuyenKhoan",
                table: "GiaoDichChuyenKhoans",
                column: "NoiDungChuyenKhoan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThongBaoNganHangs_MaGiaoDich",
                table: "ThongBaoNganHangs",
                column: "MaGiaoDich");

            migrationBuilder.CreateIndex(
                name: "IX_ThongBaoNganHangs_MaGiaoDichNganHang",
                table: "ThongBaoNganHangs",
                column: "MaGiaoDichNganHang",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThongBaoNganHangs");

            migrationBuilder.DropTable(
                name: "GiaoDichChuyenKhoans");
        }
    }
}
