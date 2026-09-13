using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DichVus",
                columns: table => new
                {
                    MaDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenDV = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GiaDV = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DonViTinh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DichVus", x => x.MaDV);
                });

            migrationBuilder.CreateTable(
                name: "Khachs",
                columns: table => new
                {
                    MaKhach = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoDT = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    CCCD = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Khachs", x => x.MaKhach);
                });

            migrationBuilder.CreateTable(
                name: "KhuyenMais",
                columns: table => new
                {
                    MaKM = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenKM = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhanTramKM = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    NgayBatDau = table.Column<DateOnly>(type: "date", nullable: false),
                    NgayKetThuc = table.Column<DateOnly>(type: "date", nullable: false),
                    DieuKien = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LoaiKM = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GiaTri = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SoLuongGioiHan = table.Column<int>(type: "int", nullable: true),
                    SoLuongDaSuDung = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhuyenMais", x => x.MaKM);
                });

            migrationBuilder.CreateTable(
                name: "LoaiPhongs",
                columns: table => new
                {
                    MaLoai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenLoai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SoNguoiTieuChuan = table.Column<int>(type: "int", nullable: false),
                    DonGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PhuThu = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoaiPhongs", x => x.MaLoai);
                });

            migrationBuilder.CreateTable(
                name: "Quyens",
                columns: table => new
                {
                    MaQuyen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenQuyen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NhomChucNang = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quyens", x => x.MaQuyen);
                });

            migrationBuilder.CreateTable(
                name: "VaiTros",
                columns: table => new
                {
                    MaVaiTro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenVaiTro = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaiTros", x => x.MaVaiTro);
                });

            migrationBuilder.CreateTable(
                name: "PhieuDatPhongs",
                columns: table => new
                {
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaKhach = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NgayDat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgayDonDuKien = table.Column<DateOnly>(type: "date", nullable: false),
                    NgayTraDuKien = table.Column<DateOnly>(type: "date", nullable: false),
                    TienCoc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LoaiDatPhong = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuDatPhongs", x => x.MaPhieuDat);
                    table.ForeignKey(
                        name: "FK_PhieuDatPhongs_Khachs_MaKhach",
                        column: x => x.MaKhach,
                        principalTable: "Khachs",
                        principalColumn: "MaKhach",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Phongs",
                columns: table => new
                {
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TenPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Tang = table.Column<int>(type: "int", nullable: false),
                    TinhTrang = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: false),
                    MaLoai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phongs", x => x.MaPhong);
                    table.ForeignKey(
                        name: "FK_Phongs_LoaiPhongs_MaLoai",
                        column: x => x.MaLoai,
                        principalTable: "LoaiPhongs",
                        principalColumn: "MaLoai",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhanViens",
                columns: table => new
                {
                    MaNV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoDT = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    ChucVu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaVaiTro = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhanViens", x => x.MaNV);
                    table.ForeignKey(
                        name: "FK_NhanViens_VaiTros_MaVaiTro",
                        column: x => x.MaVaiTro,
                        principalTable: "VaiTros",
                        principalColumn: "MaVaiTro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VaiTro_Quyen",
                columns: table => new
                {
                    MaVaiTro = table.Column<int>(type: "int", nullable: false),
                    MaQuyen = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaiTro_Quyen", x => new { x.MaVaiTro, x.MaQuyen });
                    table.ForeignKey(
                        name: "FK_VaiTro_Quyen_Quyens_MaQuyen",
                        column: x => x.MaQuyen,
                        principalTable: "Quyens",
                        principalColumn: "MaQuyen",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaiTro_Quyen_VaiTros_MaVaiTro",
                        column: x => x.MaVaiTro,
                        principalTable: "VaiTros",
                        principalColumn: "MaVaiTro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietKhuyenMais",
                columns: table => new
                {
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaKM = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TongKhuyenMai = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietKhuyenMais", x => new { x.MaPhieuDat, x.MaKM });
                    table.ForeignKey(
                        name: "FK_ChiTietKhuyenMais_KhuyenMais_MaKM",
                        column: x => x.MaKM,
                        principalTable: "KhuyenMais",
                        principalColumn: "MaKM",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietKhuyenMais_PhieuDatPhongs_MaPhieuDat",
                        column: x => x.MaPhieuDat,
                        principalTable: "PhieuDatPhongs",
                        principalColumn: "MaPhieuDat",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhieuNhanPhongs",
                columns: table => new
                {
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: true),
                    NgayNhan = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuNhanPhongs", x => x.MaPhieuNhan);
                    table.ForeignKey(
                        name: "FK_PhieuNhanPhongs_PhieuDatPhongs_MaPhieuDat",
                        column: x => x.MaPhieuDat,
                        principalTable: "PhieuDatPhongs",
                        principalColumn: "MaPhieuDat",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietPhieuDats",
                columns: table => new
                {
                    MaPhieuDat = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DonGiaApDung = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietPhieuDats", x => new { x.MaPhieuDat, x.MaPhong });
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuDats_PhieuDatPhongs_MaPhieuDat",
                        column: x => x.MaPhieuDat,
                        principalTable: "PhieuDatPhongs",
                        principalColumn: "MaPhieuDat",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuDats_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoSoVatChats",
                columns: table => new
                {
                    MaSo = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TinhTrang = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoSoVatChats", x => x.MaSo);
                    table.ForeignKey(
                        name: "FK_CoSoVatChats_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoans",
                columns: table => new
                {
                    TenDN = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MatKhau = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    LoaiTaiKhoan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaNV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    MaKhach = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "HOAT_DONG"),
                    SoLanDangNhapSai = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RefreshToken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoans", x => x.TenDN);
                    table.ForeignKey(
                        name: "FK_TaiKhoans_Khachs_MaKhach",
                        column: x => x.MaKhach,
                        principalTable: "Khachs",
                        principalColumn: "MaKhach",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaiKhoans_NhanViens_MaNV",
                        column: x => x.MaNV,
                        principalTable: "NhanViens",
                        principalColumn: "MaNV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietPhieuNhans",
                columns: table => new
                {
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhong = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NgayNhan = table.Column<DateOnly>(type: "date", nullable: false),
                    NgayTra = table.Column<DateOnly>(type: "date", nullable: true),
                    SoNguoi = table.Column<int>(type: "int", nullable: false),
                    DonGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienPhuThu = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietPhieuNhans", x => new { x.MaPhieuNhan, x.MaPhong });
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhans_PhieuNhanPhongs_MaPhieuNhan",
                        column: x => x.MaPhieuNhan,
                        principalTable: "PhieuNhanPhongs",
                        principalColumn: "MaPhieuNhan",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietPhieuNhans_Phongs_MaPhong",
                        column: x => x.MaPhong,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietSuDungDVs",
                columns: table => new
                {
                    MaChiTietDV = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    NgaySuDung = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietSuDungDVs", x => x.MaChiTietDV);
                    table.ForeignKey(
                        name: "FK_ChiTietSuDungDVs_DichVus_MaDV",
                        column: x => x.MaDV,
                        principalTable: "DichVus",
                        principalColumn: "MaDV",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChiTietSuDungDVs_PhieuNhanPhongs_MaPhieuNhan",
                        column: x => x.MaPhieuNhan,
                        principalTable: "PhieuNhanPhongs",
                        principalColumn: "MaPhieuNhan",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoaDons",
                columns: table => new
                {
                    MaHD = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaNV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NgayLap = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TienPhong = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TienDV = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PhuThu = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TienDaCoc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TongTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HinhThucThanhToan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TrangThaiThanhToan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "CHUA_THANH_TOAN")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaDons", x => x.MaHD);
                    table.ForeignKey(
                        name: "FK_HoaDons_NhanViens_MaNV",
                        column: x => x.MaNV,
                        principalTable: "NhanViens",
                        principalColumn: "MaNV",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoaDons_PhieuNhanPhongs_MaPhieuNhan",
                        column: x => x.MaPhieuNhan,
                        principalTable: "PhieuNhanPhongs",
                        principalColumn: "MaPhieuNhan",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LichSuSaoLuus",
                columns: table => new
                {
                    MaLichSu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoaiThaoTac = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ThoiGianThucHien = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    MaTaiKhoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    DuongDanFile = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    KetQua = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuSaoLuus", x => x.MaLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuSaoLuus_TaiKhoans_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyBanGiaoCas",
                columns: table => new
                {
                    MaBanGiao = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoanGiao = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    MaTaiKhoanNhan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    ThoiGianBanGiao = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    TongTienMatDauCa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TongTienMatCuoiCa = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SoLuongPhieuTrongCa = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyBanGiaoCas", x => x.MaBanGiao);
                    table.ForeignKey(
                        name: "FK_NhatKyBanGiaoCas_TaiKhoans_MaTaiKhoanGiao",
                        column: x => x.MaTaiKhoanGiao,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NhatKyBanGiaoCas_TaiKhoans_MaTaiKhoanNhan",
                        column: x => x.MaTaiKhoanNhan,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyThaoTacs",
                columns: table => new
                {
                    MaNhatKy = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiKhoan = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    HanhDong = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    DoiTuongTacDong = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DiaChiIP = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    ChiTiet = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyThaoTacs", x => x.MaNhatKy);
                    table.ForeignKey(
                        name: "FK_NhatKyThaoTacs_TaiKhoans_MaTaiKhoan",
                        column: x => x.MaTaiKhoan,
                        principalTable: "TaiKhoans",
                        principalColumn: "TenDN",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhieuDoiPhongs",
                columns: table => new
                {
                    MaPhieuDoi = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhieuNhan = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    MaPhongCu = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaPhongMoi = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NgayDoi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChenhLechGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuDoiPhongs", x => x.MaPhieuDoi);
                    table.ForeignKey(
                        name: "FK_PhieuDoiPhongs_ChiTietPhieuNhans_MaPhieuNhan_MaPhongCu",
                        columns: x => new { x.MaPhieuNhan, x.MaPhongCu },
                        principalTable: "ChiTietPhieuNhans",
                        principalColumns: new[] { "MaPhieuNhan", "MaPhong" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhieuDoiPhongs_Phongs_MaPhongMoi",
                        column: x => x.MaPhongMoi,
                        principalTable: "Phongs",
                        principalColumn: "MaPhong",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietHoaDons",
                columns: table => new
                {
                    MaChiTietHoaDon = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaHD = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    LoaiKhoanMuc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DonGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThanhTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietHoaDons", x => x.MaChiTietHoaDon);
                    table.ForeignKey(
                        name: "FK_ChiTietHoaDons_HoaDons_MaHD",
                        column: x => x.MaHD,
                        principalTable: "HoaDons",
                        principalColumn: "MaHD",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietThanhToans",
                columns: table => new
                {
                    MaThanhToan = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaHD = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    HinhThucThanhToan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThoiGianThanhToan = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaTaiKhoanThuNgan = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaGiaoDich = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietThanhToans", x => x.MaThanhToan);
                    table.ForeignKey(
                        name: "FK_ChiTietThanhToans_HoaDons_MaHD",
                        column: x => x.MaHD,
                        principalTable: "HoaDons",
                        principalColumn: "MaHD",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChiTietThanhToans_NhanViens_MaTaiKhoanThuNgan",
                        column: x => x.MaTaiKhoanThuNgan,
                        principalTable: "NhanViens",
                        principalColumn: "MaNV",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietHoaDons_MaHD",
                table: "ChiTietHoaDons",
                column: "MaHD");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietKhuyenMais_MaKM",
                table: "ChiTietKhuyenMais",
                column: "MaKM");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuDats_MaPhong",
                table: "ChiTietPhieuDats",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhieuNhans_MaPhong",
                table: "ChiTietPhieuNhans",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietSuDungDVs_MaDV",
                table: "ChiTietSuDungDVs",
                column: "MaDV");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietSuDungDVs_MaPhieuNhan",
                table: "ChiTietSuDungDVs",
                column: "MaPhieuNhan");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThanhToans_MaHD",
                table: "ChiTietThanhToans",
                column: "MaHD");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietThanhToans_MaTaiKhoanThuNgan",
                table: "ChiTietThanhToans",
                column: "MaTaiKhoanThuNgan");

            migrationBuilder.CreateIndex(
                name: "IX_CoSoVatChats_MaPhong",
                table: "CoSoVatChats",
                column: "MaPhong");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDons_MaNV",
                table: "HoaDons",
                column: "MaNV");

            migrationBuilder.CreateIndex(
                name: "IX_HoaDons_MaPhieuNhan",
                table: "HoaDons",
                column: "MaPhieuNhan");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuSaoLuus_MaTaiKhoan",
                table: "LichSuSaoLuus",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_NhanViens_MaVaiTro",
                table: "NhanViens",
                column: "MaVaiTro");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyBanGiaoCas_MaTaiKhoanGiao",
                table: "NhatKyBanGiaoCas",
                column: "MaTaiKhoanGiao");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyBanGiaoCas_MaTaiKhoanNhan",
                table: "NhatKyBanGiaoCas",
                column: "MaTaiKhoanNhan");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTacs_MaTaiKhoan",
                table: "NhatKyThaoTacs",
                column: "MaTaiKhoan");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuDatPhongs_MaKhach",
                table: "PhieuDatPhongs",
                column: "MaKhach");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuDoiPhongs_MaPhieuNhan_MaPhongCu",
                table: "PhieuDoiPhongs",
                columns: new[] { "MaPhieuNhan", "MaPhongCu" });

            migrationBuilder.CreateIndex(
                name: "IX_PhieuDoiPhongs_MaPhongMoi",
                table: "PhieuDoiPhongs",
                column: "MaPhongMoi");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuNhanPhongs_MaPhieuDat",
                table: "PhieuNhanPhongs",
                column: "MaPhieuDat",
                unique: true,
                filter: "[MaPhieuDat] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Phongs_MaLoai",
                table: "Phongs",
                column: "MaLoai");

            migrationBuilder.CreateIndex(
                name: "IX_Quyens_TenQuyen",
                table: "Quyens",
                column: "TenQuyen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_MaKhach",
                table: "TaiKhoans",
                column: "MaKhach",
                unique: true,
                filter: "[MaKhach] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_MaNV",
                table: "TaiKhoans",
                column: "MaNV",
                unique: true,
                filter: "[MaNV] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VaiTro_Quyen_MaQuyen",
                table: "VaiTro_Quyen",
                column: "MaQuyen");

            migrationBuilder.CreateIndex(
                name: "IX_VaiTros_TenVaiTro",
                table: "VaiTros",
                column: "TenVaiTro",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiTietHoaDons");

            migrationBuilder.DropTable(
                name: "ChiTietKhuyenMais");

            migrationBuilder.DropTable(
                name: "ChiTietPhieuDats");

            migrationBuilder.DropTable(
                name: "ChiTietSuDungDVs");

            migrationBuilder.DropTable(
                name: "ChiTietThanhToans");

            migrationBuilder.DropTable(
                name: "CoSoVatChats");

            migrationBuilder.DropTable(
                name: "LichSuSaoLuus");

            migrationBuilder.DropTable(
                name: "NhatKyBanGiaoCas");

            migrationBuilder.DropTable(
                name: "NhatKyThaoTacs");

            migrationBuilder.DropTable(
                name: "PhieuDoiPhongs");

            migrationBuilder.DropTable(
                name: "VaiTro_Quyen");

            migrationBuilder.DropTable(
                name: "KhuyenMais");

            migrationBuilder.DropTable(
                name: "DichVus");

            migrationBuilder.DropTable(
                name: "HoaDons");

            migrationBuilder.DropTable(
                name: "TaiKhoans");

            migrationBuilder.DropTable(
                name: "ChiTietPhieuNhans");

            migrationBuilder.DropTable(
                name: "Quyens");

            migrationBuilder.DropTable(
                name: "NhanViens");

            migrationBuilder.DropTable(
                name: "PhieuNhanPhongs");

            migrationBuilder.DropTable(
                name: "Phongs");

            migrationBuilder.DropTable(
                name: "VaiTros");

            migrationBuilder.DropTable(
                name: "PhieuDatPhongs");

            migrationBuilder.DropTable(
                name: "LoaiPhongs");

            migrationBuilder.DropTable(
                name: "Khachs");
        }
    }
}
