using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemQuanLyDieuHanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LanDangNhapCuoi",
                table: "TaiKhoans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuyBoi",
                table: "PhieuDatPhongs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyDoHuy",
                table: "PhieuDatPhongs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayHuy",
                table: "PhieuDatPhongs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguonDat",
                table: "PhieuDatPhongs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "QUAY_LE_TAN");

            migrationBuilder.AddColumn<decimal>(
                name: "SoTienHoanCoc",
                table: "PhieuDatPhongs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TienCocGiuLai",
                table: "PhieuDatPhongs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "LichSuTinhTrangPhongs",
                columns: table => new
                {
                    MaLichSu = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TuTinhTrang = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    DenTinhTrang = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaTaiKhoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    LyDo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuTinhTrangPhongs", x => x.MaLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuTinhTrangPhongs_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuyTacGias",
                columns: table => new
                {
                    MaQuyTac = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenQuyTac = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoaiQuyTac = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MaLoai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    KieuDieuChinh = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    GiaTri = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TuNgay = table.Column<DateOnly>(type: "date", nullable: true),
                    DenNgay = table.Column<DateOnly>(type: "date", nullable: true),
                    CacThuApDung = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    LoaiDatPhongApDung = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    UuTien = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaoBoi = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuyTacGias", x => x.MaQuyTac);
                    table.ForeignKey(
                        name: "FK_QuyTacGias_LoaiPhongs_MaLoai",
                        column: x => x.MaLoai,
                        principalTable: "LoaiPhongs",
                        principalColumn: "MaLoai",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauPheDuyets",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Loai = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    MaThamChieu = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ThamSo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TaiKhoanYeuCau = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NgayYeuCau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaiKhoanXuLy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NgayXuLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauPheDuyets", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauPheDuyets_TaiKhoans_TaiKhoanXuLy",
                        column: x => x.TaiKhoanXuLy,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauPheDuyets_TaiKhoans_TaiKhoanYeuCau",
                        column: x => x.TaiKhoanYeuCau,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTinhTrangPhongs_MaPhong_ThoiGian",
                table: "LichSuTinhTrangPhongs",
                columns: new[] { "MaPhong", "ThoiGian" });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuTinhTrangPhongs_ThoiGian",
                table: "LichSuTinhTrangPhongs",
                column: "ThoiGian");

            migrationBuilder.CreateIndex(
                name: "IX_QuyTacGias_MaLoai",
                table: "QuyTacGias",
                column: "MaLoai");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauPheDuyets_Loai_MaThamChieu",
                table: "YeuCauPheDuyets",
                columns: new[] { "Loai", "MaThamChieu" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauPheDuyets_TaiKhoanXuLy",
                table: "YeuCauPheDuyets",
                column: "TaiKhoanXuLy");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauPheDuyets_TaiKhoanYeuCau",
                table: "YeuCauPheDuyets",
                column: "TaiKhoanYeuCau");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauPheDuyets_TrangThai_NgayYeuCau",
                table: "YeuCauPheDuyets",
                columns: new[] { "TrangThai", "NgayYeuCau" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichSuTinhTrangPhongs");

            migrationBuilder.DropTable(
                name: "QuyTacGias");

            migrationBuilder.DropTable(
                name: "YeuCauPheDuyets");

            migrationBuilder.DropColumn(
                name: "LanDangNhapCuoi",
                table: "TaiKhoans");

            migrationBuilder.DropColumn(
                name: "HuyBoi",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "LyDoHuy",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "NgayHuy",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "NguonDat",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "SoTienHoanCoc",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "TienCocGiuLai",
                table: "PhieuDatPhongs");
        }
    }
}
