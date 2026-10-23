using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GoXacMinhEmailThemHoaDonDienTu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauXacMinhEmails");

            migrationBuilder.DropColumn(
                name: "EmailDaXacMinh",
                table: "Khachs");

            migrationBuilder.DropColumn(
                name: "NgayXacMinhEmail",
                table: "Khachs");

            migrationBuilder.CreateTable(
                name: "HoaDonDienTus",
                columns: table => new
                {
                    MaHDDT = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaHD = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TenNguoiMua = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenDonVi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MaSoThue = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ThueSuat = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TienChuaThue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienThue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TongThanhToan = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NhaCungCap = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    MauSo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    KyHieu = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    SoHoaDon = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    MaCQT = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    NgayPhatHanh = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaTraCuu = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: true),
                    LienKetTraCuu = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    MaThamChieuNhaCungCap = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ThongBaoLoi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SoLanThu = table.Column<int>(type: "int", nullable: false),
                    NguoiYeuCau = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayCapNhat = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaDonDienTus", x => x.MaHDDT);
                    table.ForeignKey(
                        name: "FK_HoaDonDienTus_HoaDons_MaHD",
                        column: x => x.MaHD,
                        principalTable: "HoaDons",
                        principalColumn: "MaHD",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HoaDonDienTus_MaHD",
                table: "HoaDonDienTus",
                column: "MaHD",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoaDonDienTus");

            migrationBuilder.AddColumn<bool>(
                name: "EmailDaXacMinh",
                table: "Khachs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayXacMinhEmail",
                table: "Khachs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "YeuCauXacMinhEmails",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaKhach = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    HanDung = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgaySuDung = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauXacMinhEmails", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauXacMinhEmails_Khachs_MaKhach",
                        column: x => x.MaKhach,
                        principalTable: "Khachs",
                        principalColumn: "MaKhach",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauXacMinhEmails_MaKhach_NgayTao",
                table: "YeuCauXacMinhEmails",
                columns: new[] { "MaKhach", "NgayTao" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauXacMinhEmails_TokenHash",
                table: "YeuCauXacMinhEmails",
                column: "TokenHash",
                unique: true);
        }
    }
}
