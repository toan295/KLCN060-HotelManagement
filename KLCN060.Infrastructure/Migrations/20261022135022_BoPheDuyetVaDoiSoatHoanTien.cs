using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoPheDuyetVaDoiSoatHoanTien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoanTiens");

            migrationBuilder.DropTable(
                name: "PhieuKiemQuys");

            migrationBuilder.DropTable(
                name: "YeuCauPheDuyets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HoanTiens",
                columns: table => new
                {
                    MaHoanTien = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    MaThongBao = table.Column<int>(type: "int", nullable: true),
                    GhiChuHoan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HinhThucHoan = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Loai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MaChungTu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaYeuCau = table.Column<int>(type: "int", nullable: true),
                    NgayHoan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaiKhoanHoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TaiKhoanTao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoanTiens", x => x.MaHoanTien);
                    table.ForeignKey(
                        name: "FK_HoanTiens_PhieuDatPhongs_MaPhieuDat",
                        column: x => x.MaPhieuDat,
                        principalTable: "PhieuDatPhongs",
                        principalColumn: "MaPhieuDat",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoanTiens_ThongBaoNganHangs_MaThongBao",
                        column: x => x.MaThongBao,
                        principalTable: "ThongBaoNganHangs",
                        principalColumn: "MaThongBao",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhieuKiemQuys",
                columns: table => new
                {
                    MaKiemQuy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChenhLech = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LyDoHuy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaNVThuNgan = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    NgayHuy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayKiemQuy = table.Column<DateOnly>(type: "date", nullable: false),
                    NgayLap = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoTienHeThong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoTienThucTe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaiKhoanHuy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TaiKhoanLap = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuKiemQuys", x => x.MaKiemQuy);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauPheDuyets",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaiKhoanXuLy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    TaiKhoanYeuCau = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Loai = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MaThamChieu = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NgayXuLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayYeuCau = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ThamSo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
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
                name: "IX_HoanTiens_MaPhieuDat",
                table: "HoanTiens",
                column: "MaPhieuDat",
                unique: true,
                filter: "[Loai] = 'HOAN_COC'");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_MaThongBao",
                table: "HoanTiens",
                column: "MaThongBao");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_TrangThai",
                table: "HoanTiens",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuKiemQuys_NgayKiemQuy",
                table: "PhieuKiemQuys",
                column: "NgayKiemQuy");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuKiemQuys_NgayKiemQuy_MaNVThuNgan",
                table: "PhieuKiemQuys",
                columns: new[] { "NgayKiemQuy", "MaNVThuNgan" },
                unique: true,
                filter: "[TrangThai] = 'HIEU_LUC'");

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
    }
}
