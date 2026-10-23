using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemTaiChinhKeToan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GhiChuXuLy",
                table: "ThongBaoNganHangs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuongXuLy",
                table: "ThongBaoNganHangs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayXuLy",
                table: "ThongBaoNganHangs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiXuLy",
                table: "ThongBaoNganHangs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrangThaiXuLy",
                table: "ThongBaoNganHangs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "KHONG_CAN");

            migrationBuilder.AddColumn<string>(
                name: "HinhThucCoc",
                table: "PhieuDatPhongs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayNhanCoc",
                table: "PhieuDatPhongs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiNhanCoc",
                table: "PhieuDatPhongs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoaiDoanhThu",
                table: "DichVus",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "DICH_VU");

            // Phân loại doanh thu cho dịch vụ đã có (chạy một lần): dịch vụ gắn với đồ minibar của buồng phòng hoặc tên có "minibar" -> MINIBAR; tên có "giặt" -> GIAT_LA.
            migrationBuilder.Sql(@"
                UPDATE DichVus SET LoaiDoanhThu = 'MINIBAR'
                WHERE MaDV IN (SELECT MaDV FROM VatDungs WHERE Nhom = 'MINIBAR' AND MaDV IS NOT NULL) OR TenDV LIKE N'%minibar%';
                UPDATE DichVus SET LoaiDoanhThu = 'GIAT_LA' WHERE LoaiDoanhThu = 'DICH_VU' AND (TenDV LIKE N'%giặt%' OR TenDV LIKE N'%giat%');");

            migrationBuilder.CreateTable(
                name: "HoanTiens",
                columns: table => new
                {
                    MaHoanTien = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Loai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    MaThongBao = table.Column<int>(type: "int", nullable: true),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    LyDo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TaiKhoanTao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaYeuCau = table.Column<int>(type: "int", nullable: true),
                    TaiKhoanHoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NgayHoan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HinhThucHoan = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    MaChungTu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GhiChuHoan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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
                    NgayKiemQuy = table.Column<DateOnly>(type: "date", nullable: false),
                    MaNVThuNgan = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    SoTienHeThong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoTienThucTe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChenhLech = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaiKhoanLap = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NgayLap = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TaiKhoanHuy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    NgayHuy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LyDoHuy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuKiemQuys", x => x.MaKiemQuy);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThongBaoNganHangs_TrangThaiXuLy",
                table: "ThongBaoNganHangs",
                column: "TrangThaiXuLy");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoanTiens");

            migrationBuilder.DropTable(
                name: "PhieuKiemQuys");

            migrationBuilder.DropIndex(
                name: "IX_ThongBaoNganHangs_TrangThaiXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "GhiChuXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "HuongXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "NgayXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "NguoiXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "TrangThaiXuLy",
                table: "ThongBaoNganHangs");

            migrationBuilder.DropColumn(
                name: "HinhThucCoc",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "NgayNhanCoc",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "NguoiNhanCoc",
                table: "PhieuDatPhongs");

            migrationBuilder.DropColumn(
                name: "LoaiDoanhThu",
                table: "DichVus");
        }
    }
}
