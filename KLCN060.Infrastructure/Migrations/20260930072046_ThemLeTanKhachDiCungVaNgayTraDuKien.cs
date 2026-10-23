using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemLeTanKhachDiCungVaNgayTraDuKien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NgayTraDuKien",
                table: "ChiTietPhieuNhans",
                type: "date",
                nullable: true);

            // Lượt ở đã có từ trước: lấy ngày trả dự kiến từ phiếu đặt (lượt vãng lai không có phiếu đặt nên để trống).
            migrationBuilder.Sql(@"UPDATE c SET c.NgayTraDuKien = p.NgayTraDuKien
FROM ChiTietPhieuNhans c
JOIN PhieuNhanPhongs n ON n.MaPhieuNhan = c.MaPhieuNhan
JOIN PhieuDatPhongs p ON p.MaPhieuDat = n.MaPhieuDat");

            migrationBuilder.CreateTable(
                name: "KhachDiCungs",
                columns: table => new
                {
                    MaKhachDiCung = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CCCD = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    NgayThem = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiThem = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhachDiCungs", x => x.MaKhachDiCung);
                    table.ForeignKey(
                        name: "FK_KhachDiCungs_PhieuDatPhongs_MaPhieuDat",
                        column: x => x.MaPhieuDat,
                        principalTable: "PhieuDatPhongs",
                        principalColumn: "MaPhieuDat",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhachDiCungs_MaPhieuDat",
                table: "KhachDiCungs",
                column: "MaPhieuDat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KhachDiCungs");

            migrationBuilder.DropColumn(
                name: "NgayTraDuKien",
                table: "ChiTietPhieuNhans");
        }
    }
}
