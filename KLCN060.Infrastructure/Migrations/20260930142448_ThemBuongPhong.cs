using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemBuongPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoBoQuens",
                columns: table => new
                {
                    MaDo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    KhuVuc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ThoiGianPhatHien = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Loai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NguoiNhat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NguoiGhiNhan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ViTriLuuGiu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    ThoiGianTra = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiNhanLai = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GiayToNguoiNhan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NguoiTra = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    GhiChuTra = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ThoiGianThanhLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiThanhLy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    LyDoThanhLy = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoBoQuens", x => x.MaDo);
                    table.ForeignKey(
                        name: "FK_DoBoQuens_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhieuDonPhongs",
                columns: table => new
                {
                    MaPhieuDon = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NguoiDon = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TinhTrangBanDau = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    ThoiGianBatDau = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThoiGianXong = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuDonPhongs", x => x.MaPhieuDon);
                    table.ForeignKey(
                        name: "FK_PhieuDonPhongs_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhieuKiemPhongs",
                columns: table => new
                {
                    MaPhieuKiem = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    NguoiKiem = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuKiemPhongs", x => x.MaPhieuKiem);
                    table.ForeignKey(
                        name: "FK_PhieuKiemPhongs_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuCoPhongs",
                columns: table => new
                {
                    MaSuCo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Loai = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MucDo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TrangThai = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NguoiBao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ThoiGianBao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NguoiXuLy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ThoiGianTiepNhan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThoiGianXong = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuCoPhongs", x => x.MaSuCo);
                    table.ForeignKey(
                        name: "FK_SuCoPhongs_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VatDungs",
                columns: table => new
                {
                    MaVatDung = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenVatDung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nhom = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DonViTinh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SoLuongChuan = table.Column<int>(type: "int", nullable: false),
                    MaDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    HoatDong = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatDungs", x => x.MaVatDung);
                    table.ForeignKey(
                        name: "FK_VatDungs_DichVus_MaDV",
                        column: x => x.MaDV,
                        principalTable: "DichVus",
                        principalColumn: "MaDV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnhDoBoQuens",
                columns: table => new
                {
                    MaAnh = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDo = table.Column<int>(type: "int", nullable: false),
                    TenFile = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    KichThuocByte = table.Column<long>(type: "bigint", nullable: false),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnhDoBoQuens", x => x.MaAnh);
                    table.ForeignKey(
                        name: "FK_AnhDoBoQuens_DoBoQuens_MaDo",
                        column: x => x.MaDo,
                        principalTable: "DoBoQuens",
                        principalColumn: "MaDo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietKiemPhongs",
                columns: table => new
                {
                    MaChiTiet = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieuKiem = table.Column<int>(type: "int", nullable: false),
                    MaVatDung = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SoLuongTruoc = table.Column<int>(type: "int", nullable: false),
                    SoLuongSau = table.Column<int>(type: "int", nullable: false),
                    SoLuongBoSung = table.Column<int>(type: "int", nullable: false),
                    LyDo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SoLuongTinhPhi = table.Column<int>(type: "int", nullable: false),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrangThaiTinhPhi = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MaChiTietDV = table.Column<int>(type: "int", nullable: true),
                    NguoiXuLy = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ThoiGianXuLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LyDoBoQua = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietKiemPhongs", x => x.MaChiTiet);
                    table.ForeignKey(
                        name: "FK_ChiTietKiemPhongs_PhieuKiemPhongs_MaPhieuKiem",
                        column: x => x.MaPhieuKiem,
                        principalTable: "PhieuKiemPhongs",
                        principalColumn: "MaPhieuKiem",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChiTietKiemPhongs_VatDungs_MaVatDung",
                        column: x => x.MaVatDung,
                        principalTable: "VatDungs",
                        principalColumn: "MaVatDung",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnhDoBoQuens_MaDo",
                table: "AnhDoBoQuens",
                column: "MaDo");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietKiemPhongs_MaPhieuKiem",
                table: "ChiTietKiemPhongs",
                column: "MaPhieuKiem");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietKiemPhongs_MaVatDung",
                table: "ChiTietKiemPhongs",
                column: "MaVatDung");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietKiemPhongs_TrangThaiTinhPhi",
                table: "ChiTietKiemPhongs",
                column: "TrangThaiTinhPhi");

            migrationBuilder.CreateIndex(
                name: "IX_DoBoQuens_MaPhong",
                table: "DoBoQuens",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_DoBoQuens_ThoiGianPhatHien",
                table: "DoBoQuens",
                column: "ThoiGianPhatHien");

            migrationBuilder.CreateIndex(
                name: "IX_DoBoQuens_TrangThai",
                table: "DoBoQuens",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuDonPhongs_MaPhong",
                table: "PhieuDonPhongs",
                column: "MaPhong",
                unique: true,
                filter: "[TrangThai] = 'DANG_DON'");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuDonPhongs_ThoiGianXong",
                table: "PhieuDonPhongs",
                column: "ThoiGianXong");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuKiemPhongs_MaPhong_ThoiGian",
                table: "PhieuKiemPhongs",
                columns: new[] { "MaPhong", "ThoiGian" });

            migrationBuilder.CreateIndex(
                name: "IX_SuCoPhongs_MaPhong_TrangThai",
                table: "SuCoPhongs",
                columns: new[] { "MaPhong", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_SuCoPhongs_TrangThai",
                table: "SuCoPhongs",
                column: "TrangThai");

            migrationBuilder.CreateIndex(
                name: "IX_VatDungs_MaDV",
                table: "VatDungs",
                column: "MaDV");

            migrationBuilder.CreateIndex(
                name: "IX_VatDungs_TenVatDung",
                table: "VatDungs",
                column: "TenVatDung",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnhDoBoQuens");

            migrationBuilder.DropTable(
                name: "ChiTietKiemPhongs");

            migrationBuilder.DropTable(
                name: "PhieuDonPhongs");

            migrationBuilder.DropTable(
                name: "SuCoPhongs");

            migrationBuilder.DropTable(
                name: "DoBoQuens");

            migrationBuilder.DropTable(
                name: "PhieuKiemPhongs");

            migrationBuilder.DropTable(
                name: "VatDungs");
        }
    }
}
