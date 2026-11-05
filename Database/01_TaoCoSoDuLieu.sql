/* =====================================================================================================
   KLCN060 - Hệ thống quản lý khách sạn
   Script 01: Tạo cơ sở dữ liệu và toàn bộ bảng, khóa chính / khóa ngoại / chỉ mục.

   ĐÂY LÀ NGUỒN GỐC (SOURCE OF TRUTH) CỦA CẤU TRÚC DỮ LIỆU - hướng DATABASE FIRST:
     * Cấu trúc bảng do script SQL trong thư mục Database/ quy định, không do ứng dụng sinh ra.
     * API (Entity Framework Core) chỉ ÁNH XẠ vào cơ sở dữ liệu đã có sẵn; không còn Migration và không tự tạo / sửa bảng.
     * Muốn đổi cấu trúc: sửa script SQL (hoặc thêm script ALTER mới) -> chạy lại -> cập nhật lớp ánh xạ trong
       KLCN060.Domain / KLCN060.Infrastructure cho khớp (có thể dùng dotnet ef dbcontext scaffold để đối chiếu).

   Cách chạy (SQL Server / LocalDB):  sqlcmd -S "(localdb)\mssqllocaldb" -i Database\01_TaoCoSoDuLieu.sql
   Hoặc mở bằng SSMS / Azure Data Studio rồi Execute. Chạy lần lượt 01 -> 02 -> 03 -> 04 (xem Database/README.md).
   ===================================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_ID(N'KLCN060_QuanLyKhachSan') IS NULL
    CREATE DATABASE [KLCN060_QuanLyKhachSan];
GO

USE [KLCN060_QuanLyKhachSan];
GO

CREATE TABLE [CauHoiThuongGaps] (
    [MaCauHoi] int NOT NULL IDENTITY,
    [TuKhoa] nvarchar(255) NOT NULL,
    [CauTraLoi] nvarchar(1000) NOT NULL,
    CONSTRAINT [PK_CauHoiThuongGaps] PRIMARY KEY ([MaCauHoi])
);
GO

CREATE TABLE [DichVus] (
    [MaDV] varchar(10) NOT NULL,
    [TenDV] nvarchar(100) NOT NULL,
    [GiaDV] decimal(18,2) NOT NULL,
    [DonViTinh] nvarchar(20) NOT NULL,
    [HoatDong] bit NOT NULL DEFAULT CAST(1 AS bit),
    [LoaiDoanhThu] varchar(20) NOT NULL DEFAULT 'DICH_VU',
    CONSTRAINT [PK_DichVus] PRIMARY KEY ([MaDV])
);
GO

CREATE TABLE [Khachs] (
    [MaKhach] varchar(10) NOT NULL,
    [HoTen] nvarchar(100) NOT NULL,
    [SoDT] varchar(15) NOT NULL,
    [CCCD] varchar(20) NULL,
    [Email] varchar(100) NULL,
    [DiaChi] nvarchar(255) NULL,
    CONSTRAINT [PK_Khachs] PRIMARY KEY ([MaKhach])
);
GO

CREATE TABLE [KhuyenMais] (
    [MaKM] varchar(10) NOT NULL,
    [TenKM] nvarchar(100) NOT NULL,
    [PhanTramKM] decimal(5,2) NULL,
    [NgayBatDau] date NOT NULL,
    [NgayKetThuc] date NOT NULL,
    [DieuKien] nvarchar(255) NULL,
    [LoaiKM] nvarchar(20) NOT NULL,
    [GiaTri] decimal(18,2) NULL,
    [SoLuongGioiHan] int NULL,
    [SoLuongDaSuDung] int NOT NULL DEFAULT 0,
    [TamNgung] bit NOT NULL DEFAULT CAST(0 AS bit),
    [LyDoTamNgung] nvarchar(255) NULL,
    [MaLoaiApDung] varchar(10) NULL,
    [SoDemToiThieu] int NULL,
    [TongTienToiThieu] decimal(18,2) NULL,
    CONSTRAINT [PK_KhuyenMais] PRIMARY KEY ([MaKM])
);
GO

CREATE TABLE [LoaiPhongs] (
    [MaLoai] varchar(10) NOT NULL,
    [TenLoai] nvarchar(50) NOT NULL,
    [SoNguoiTieuChuan] int NOT NULL,
    [DonGia] decimal(18,2) NOT NULL,
    [PhuThu] decimal(18,2) NOT NULL DEFAULT 0.0,
    [DienTich] decimal(6,1) NULL,
    [LoaiGiuong] nvarchar(100) NULL,
    [TienNghi] nvarchar(500) NULL,
    [MoTa] nvarchar(1000) NULL,
    CONSTRAINT [PK_LoaiPhongs] PRIMARY KEY ([MaLoai])
);
GO

CREATE TABLE [Quyens] (
    [MaQuyen] int NOT NULL IDENTITY,
    [TenQuyen] nvarchar(100) NOT NULL,
    [NhomChucNang] nvarchar(50) NULL,
    CONSTRAINT [PK_Quyens] PRIMARY KEY ([MaQuyen])
);
GO

CREATE TABLE [VaiTros] (
    [MaVaiTro] int NOT NULL IDENTITY,
    [TenVaiTro] nvarchar(50) NOT NULL,
    [MoTa] nvarchar(255) NULL,
    CONSTRAINT [PK_VaiTros] PRIMARY KEY ([MaVaiTro])
);
GO

CREATE TABLE [ChiTietDichVus] (
    [MaCTDV] varchar(10) NOT NULL,
    [MaDV] varchar(10) NOT NULL,
    [TieuDe] nvarchar(100) NOT NULL,
    [NoiDung] nvarchar(500) NOT NULL,
    [ThuTu] int NOT NULL DEFAULT 0,
    [ConHienThi] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_ChiTietDichVus] PRIMARY KEY ([MaCTDV]),
    CONSTRAINT [FK_ChiTietDichVus_DichVus_MaDV] FOREIGN KEY ([MaDV]) REFERENCES [DichVus] ([MaDV]) ON DELETE CASCADE
);
GO

CREATE TABLE [PhieuDatPhongs] (
    [MaPhieuDat] varchar(15) NOT NULL,
    [MaKhach] varchar(10) NOT NULL,
    [NgayDat] datetime2 NOT NULL,
    [NgayDonDuKien] date NOT NULL,
    [NgayTraDuKien] date NOT NULL,
    [TienCoc] decimal(18,2) NOT NULL,
    [SoKhach] int NULL,
    [NgayNhanCoc] datetime2 NULL,
    [HinhThucCoc] varchar(20) NULL,
    [NguoiNhanCoc] varchar(50) NULL,
    [TrangThai] nvarchar(20) NOT NULL,
    [LoaiDatPhong] nvarchar(10) NOT NULL,
    [NguonDat] varchar(20) NOT NULL DEFAULT 'QUAY_LE_TAN',
    [NgayHuy] datetime2 NULL,
    [LyDoHuy] nvarchar(500) NULL,
    [HuyBoi] varchar(50) NULL,
    [SoTienHoanCoc] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TienCocGiuLai] decimal(18,2) NOT NULL DEFAULT 0.0,
    CONSTRAINT [PK_PhieuDatPhongs] PRIMARY KEY ([MaPhieuDat]),
    CONSTRAINT [FK_PhieuDatPhongs_Khachs_MaKhach] FOREIGN KEY ([MaKhach]) REFERENCES [Khachs] ([MaKhach]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Phongs] (
    [MaPhong] varchar(10) NOT NULL,
    [TenPhong] varchar(10) NOT NULL,
    [Tang] int NOT NULL,
    [TinhTrang] varchar(5) NOT NULL,
    [MaLoai] varchar(10) NOT NULL,
    [NgayTao] datetime2 NULL,
    CONSTRAINT [PK_Phongs] PRIMARY KEY ([MaPhong]),
    CONSTRAINT [FK_Phongs_LoaiPhongs_MaLoai] FOREIGN KEY ([MaLoai]) REFERENCES [LoaiPhongs] ([MaLoai]) ON DELETE NO ACTION
);
GO

CREATE TABLE [NhanViens] (
    [MaNV] varchar(10) NOT NULL,
    [HoTen] nvarchar(100) NOT NULL,
    [SoDT] varchar(15) NOT NULL,
    [ChucVu] nvarchar(50) NOT NULL,
    [MaVaiTro] int NOT NULL,
    CONSTRAINT [PK_NhanViens] PRIMARY KEY ([MaNV]),
    CONSTRAINT [FK_NhanViens_VaiTros_MaVaiTro] FOREIGN KEY ([MaVaiTro]) REFERENCES [VaiTros] ([MaVaiTro]) ON DELETE NO ACTION
);
GO

CREATE TABLE [VaiTro_Quyen] (
    [MaVaiTro] int NOT NULL,
    [MaQuyen] int NOT NULL,
    CONSTRAINT [PK_VaiTro_Quyen] PRIMARY KEY ([MaVaiTro], [MaQuyen]),
    CONSTRAINT [FK_VaiTro_Quyen_Quyens_MaQuyen] FOREIGN KEY ([MaQuyen]) REFERENCES [Quyens] ([MaQuyen]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VaiTro_Quyen_VaiTros_MaVaiTro] FOREIGN KEY ([MaVaiTro]) REFERENCES [VaiTros] ([MaVaiTro]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChiTietKhuyenMais] (
    [MaPhieuDat] varchar(15) NOT NULL,
    [MaKM] varchar(10) NOT NULL,
    [TongKhuyenMai] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ChiTietKhuyenMais] PRIMARY KEY ([MaPhieuDat], [MaKM]),
    CONSTRAINT [FK_ChiTietKhuyenMais_KhuyenMais_MaKM] FOREIGN KEY ([MaKM]) REFERENCES [KhuyenMais] ([MaKM]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChiTietKhuyenMais_PhieuDatPhongs_MaPhieuDat] FOREIGN KEY ([MaPhieuDat]) REFERENCES [PhieuDatPhongs] ([MaPhieuDat]) ON DELETE NO ACTION
);
GO

CREATE TABLE [KhachDiCungs] (
    [MaKhachDiCung] int NOT NULL IDENTITY,
    [MaPhieuDat] varchar(15) NOT NULL,
    [HoTen] nvarchar(100) NOT NULL,
    [CCCD] varchar(20) NULL,
    [MaPhong] varchar(10) NULL,
    [NgayThem] datetime2 NOT NULL,
    [NguoiThem] varchar(50) NULL,
    CONSTRAINT [PK_KhachDiCungs] PRIMARY KEY ([MaKhachDiCung]),
    CONSTRAINT [FK_KhachDiCungs_PhieuDatPhongs_MaPhieuDat] FOREIGN KEY ([MaPhieuDat]) REFERENCES [PhieuDatPhongs] ([MaPhieuDat]) ON DELETE CASCADE
);
GO

CREATE TABLE [PhieuNhanPhongs] (
    [MaPhieuNhan] varchar(15) NOT NULL,
    [MaPhieuDat] varchar(15) NULL,
    [NgayNhan] datetime2 NOT NULL,
    [MaKhach] varchar(10) NULL,
    CONSTRAINT [PK_PhieuNhanPhongs] PRIMARY KEY ([MaPhieuNhan]),
    CONSTRAINT [FK_PhieuNhanPhongs_Khachs_MaKhach] FOREIGN KEY ([MaKhach]) REFERENCES [Khachs] ([MaKhach]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PhieuNhanPhongs_PhieuDatPhongs_MaPhieuDat] FOREIGN KEY ([MaPhieuDat]) REFERENCES [PhieuDatPhongs] ([MaPhieuDat]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChiTietPhieuDats] (
    [MaPhieuDat] varchar(15) NOT NULL,
    [MaPhong] varchar(10) NOT NULL,
    [SoLuong] int NOT NULL DEFAULT 1,
    [DonGiaApDung] decimal(18,2) NOT NULL,
    [ThanhTien] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ChiTietPhieuDats] PRIMARY KEY ([MaPhieuDat], [MaPhong]),
    CONSTRAINT [FK_ChiTietPhieuDats_PhieuDatPhongs_MaPhieuDat] FOREIGN KEY ([MaPhieuDat]) REFERENCES [PhieuDatPhongs] ([MaPhieuDat]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChiTietPhieuDats_Phongs_MaPhong] FOREIGN KEY ([MaPhong]) REFERENCES [Phongs] ([MaPhong]) ON DELETE NO ACTION
);
GO

CREATE TABLE [CoSoVatChats] (
    [MaSo] varchar(10) NOT NULL,
    [Ten] nvarchar(100) NOT NULL,
    [SoLuong] int NOT NULL DEFAULT 1,
    [TinhTrang] nvarchar(30) NOT NULL,
    [MaPhong] varchar(10) NULL,
    [GhiChu] nvarchar(255) NULL,
    [TinhTrangTuLuc] datetime2 NULL,
    [LyDoTinhTrang] nvarchar(255) NULL,
    [NgayTao] datetime2 NULL,
    [CapNhatLuc] datetime2 NULL,
    [NguoiCapNhat] varchar(50) NULL,
    CONSTRAINT [PK_CoSoVatChats] PRIMARY KEY ([MaSo]),
    CONSTRAINT [FK_CoSoVatChats_Phongs_MaPhong] FOREIGN KEY ([MaPhong]) REFERENCES [Phongs] ([MaPhong]) ON DELETE NO ACTION
);
GO

CREATE TABLE [LichSuTinhTrangPhongs] (
    [MaLichSu] bigint NOT NULL IDENTITY,
    [MaPhong] varchar(10) NOT NULL,
    [TuTinhTrang] varchar(5) NOT NULL,
    [DenTinhTrang] varchar(5) NOT NULL,
    [ThoiGian] datetime2 NOT NULL,
    [MaTaiKhoan] varchar(50) NULL,
    [LyDo] nvarchar(255) NULL,
    CONSTRAINT [PK_LichSuTinhTrangPhongs] PRIMARY KEY ([MaLichSu]),
    CONSTRAINT [FK_LichSuTinhTrangPhongs_Phongs_MaPhong] FOREIGN KEY ([MaPhong]) REFERENCES [Phongs] ([MaPhong]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TaiKhoans] (
    [TenDN] varchar(50) NOT NULL,
    [MatKhau] varchar(255) NOT NULL,
    [LoaiTaiKhoan] nvarchar(20) NOT NULL,
    [MaNV] varchar(10) NULL,
    [MaKhach] varchar(10) NULL,
    [TrangThai] nvarchar(20) NOT NULL DEFAULT N'HOAT_DONG',
    [SoLanDangNhapSai] int NOT NULL DEFAULT 0,
    [LanDangNhapCuoi] datetime2 NULL,
    [RefreshToken] nvarchar(255) NULL,
    [RefreshTokenExpiry] datetime2 NULL,
    CONSTRAINT [PK_TaiKhoans] PRIMARY KEY ([TenDN]),
    CONSTRAINT [FK_TaiKhoans_Khachs_MaKhach] FOREIGN KEY ([MaKhach]) REFERENCES [Khachs] ([MaKhach]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TaiKhoans_NhanViens_MaNV] FOREIGN KEY ([MaNV]) REFERENCES [NhanViens] ([MaNV]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChiTietPhieuNhans] (
    [MaPhieuNhan] varchar(15) NOT NULL,
    [MaPhong] varchar(10) NOT NULL,
    [NgayNhan] date NOT NULL,
    [NgayTra] date NULL,
    [NgayTraDuKien] date NULL,
    [SoNguoi] int NOT NULL,
    [DonGia] decimal(18,2) NOT NULL,
    [TienPhuThu] decimal(18,2) NOT NULL DEFAULT 0.0,
    [SoNguoiTieuChuanChot] int NULL,
    [PhuThuNguoiThemChot] decimal(18,2) NULL,
    [TrangThai] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_ChiTietPhieuNhans] PRIMARY KEY ([MaPhieuNhan], [MaPhong]),
    CONSTRAINT [FK_ChiTietPhieuNhans_PhieuNhanPhongs_MaPhieuNhan] FOREIGN KEY ([MaPhieuNhan]) REFERENCES [PhieuNhanPhongs] ([MaPhieuNhan]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChiTietPhieuNhans_Phongs_MaPhong] FOREIGN KEY ([MaPhong]) REFERENCES [Phongs] ([MaPhong]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChiTietSuDungDVs] (
    [MaChiTietDV] int NOT NULL IDENTITY,
    [MaPhieuNhan] varchar(15) NOT NULL,
    [MaDV] varchar(10) NOT NULL,
    [SoLuong] int NOT NULL DEFAULT 1,
    [NgaySuDung] datetime2 NOT NULL,
    [ThanhTien] decimal(18,2) NOT NULL,
    [TenDichVu] nvarchar(100) NULL,
    [DonViTinh] nvarchar(20) NULL,
    [MaPhong] varchar(10) NULL,
    [NguoiGhi] varchar(50) NULL,
    CONSTRAINT [PK_ChiTietSuDungDVs] PRIMARY KEY ([MaChiTietDV]),
    CONSTRAINT [FK_ChiTietSuDungDVs_DichVus_MaDV] FOREIGN KEY ([MaDV]) REFERENCES [DichVus] ([MaDV]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChiTietSuDungDVs_PhieuNhanPhongs_MaPhieuNhan] FOREIGN KEY ([MaPhieuNhan]) REFERENCES [PhieuNhanPhongs] ([MaPhieuNhan]) ON DELETE NO ACTION
);
GO

CREATE TABLE [HoaDons] (
    [MaHD] varchar(15) NOT NULL,
    [MaPhieuNhan] varchar(15) NOT NULL,
    [MaNV] varchar(10) NOT NULL,
    [NgayLap] datetime2 NOT NULL,
    [TienPhong] decimal(18,2) NOT NULL,
    [TienDV] decimal(18,2) NOT NULL,
    [PhuThu] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TienDaCoc] decimal(18,2) NOT NULL,
    [TienKhuyenMai] decimal(18,2) NOT NULL DEFAULT 0.0,
    [TongTien] decimal(18,2) NOT NULL,
    [HinhThucThanhToan] nvarchar(20) NOT NULL,
    [TrangThaiThanhToan] nvarchar(20) NOT NULL DEFAULT N'CHUA_THANH_TOAN',
    CONSTRAINT [PK_HoaDons] PRIMARY KEY ([MaHD]),
    CONSTRAINT [FK_HoaDons_NhanViens_MaNV] FOREIGN KEY ([MaNV]) REFERENCES [NhanViens] ([MaNV]) ON DELETE NO ACTION,
    CONSTRAINT [FK_HoaDons_PhieuNhanPhongs_MaPhieuNhan] FOREIGN KEY ([MaPhieuNhan]) REFERENCES [PhieuNhanPhongs] ([MaPhieuNhan]) ON DELETE NO ACTION
);
GO

CREATE TABLE [GiaDemDatPhongs] (
    [MaPhieuDat] varchar(15) NOT NULL,
    [MaPhong] varchar(10) NOT NULL,
    [Dem] date NOT NULL,
    [GiaCoBan] decimal(18,2) NOT NULL,
    [Gia] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_GiaDemDatPhongs] PRIMARY KEY ([MaPhieuDat], [MaPhong], [Dem]),
    CONSTRAINT [FK_GiaDemDatPhongs_ChiTietPhieuDats_MaPhieuDat_MaPhong] FOREIGN KEY ([MaPhieuDat], [MaPhong]) REFERENCES [ChiTietPhieuDats] ([MaPhieuDat], [MaPhong]) ON DELETE CASCADE
);
GO

CREATE TABLE [GiaoDichChuyenKhoans] (
    [MaGiaoDich] varchar(15) NOT NULL,
    [LoaiThamChieu] nvarchar(20) NOT NULL,
    [MaThamChieu] varchar(15) NOT NULL,
    [SoTien] decimal(18,2) NOT NULL,
    [NoiDungChuyenKhoan] varchar(50) NOT NULL,
    [TrangThai] nvarchar(20) NOT NULL,
    [NgayTao] datetime2 NOT NULL,
    [HanThanhToan] datetime2 NOT NULL,
    [NgayThanhToan] datetime2 NULL,
    [MaGiaoDichNganHang] varchar(100) NULL,
    [MaTaiKhoanTao] varchar(50) NOT NULL,
    [MaNVThuNgan] varchar(10) NULL,
    CONSTRAINT [PK_GiaoDichChuyenKhoans] PRIMARY KEY ([MaGiaoDich]),
    CONSTRAINT [FK_GiaoDichChuyenKhoans_NhanViens_MaNVThuNgan] FOREIGN KEY ([MaNVThuNgan]) REFERENCES [NhanViens] ([MaNV]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GiaoDichChuyenKhoans_TaiKhoans_MaTaiKhoanTao] FOREIGN KEY ([MaTaiKhoanTao]) REFERENCES [TaiKhoans] ([TenDN]) ON DELETE NO ACTION
);
GO

CREATE TABLE [LichSuSaoLuus] (
    [MaLichSu] int NOT NULL IDENTITY,
    [LoaiThaoTac] nvarchar(20) NOT NULL,
    [ThoiGianThucHien] datetime2 NOT NULL DEFAULT (GETDATE()),
    [MaTaiKhoan] varchar(50) NOT NULL,
    [DuongDanFile] nvarchar(500) NULL,
    [KetQua] nvarchar(20) NOT NULL,
    [GhiChu] nvarchar(255) NULL,
    [TenCSDL] nvarchar(128) NULL,
    [KichThuocBytes] bigint NULL,
    [ThoiLuongMs] int NULL,
    [NguonKichHoat] varchar(20) NULL,
    [LyDo] nvarchar(255) NULL,
    [MaBanNguon] int NULL,
    [DaKiemTraHopLe] bit NULL,
    [ThongBaoLoi] nvarchar(1000) NULL,
    CONSTRAINT [PK_LichSuSaoLuus] PRIMARY KEY ([MaLichSu]),
    CONSTRAINT [FK_LichSuSaoLuus_TaiKhoans_MaTaiKhoan] FOREIGN KEY ([MaTaiKhoan]) REFERENCES [TaiKhoans] ([TenDN]) ON DELETE NO ACTION
);
GO

CREATE TABLE [NhatKyThaoTacs] (
    [MaNhatKy] bigint NOT NULL IDENTITY,
    [MaTaiKhoan] varchar(50) NOT NULL,
    [HanhDong] nvarchar(100) NOT NULL,
    [DoiTuongTacDong] nvarchar(100) NULL,
    [ThoiGian] datetime2 NOT NULL DEFAULT (GETDATE()),
    [DiaChiIP] varchar(45) NULL,
    [ChiTiet] nvarchar(max) NULL,
    [KetQua] varchar(20) NOT NULL DEFAULT 'THANH_CONG',
    [MaLoi] varchar(80) NULL,
    [ChuKy] varchar(64) NULL,
    CONSTRAINT [PK_NhatKyThaoTacs] PRIMARY KEY ([MaNhatKy]),
    CONSTRAINT [FK_NhatKyThaoTacs_TaiKhoans_MaTaiKhoan] FOREIGN KEY ([MaTaiKhoan]) REFERENCES [TaiKhoans] ([TenDN]) ON DELETE NO ACTION
);
GO

CREATE TABLE [YeuCauDatLaiMatKhaus] (
    [MaYeuCau] int NOT NULL IDENTITY,
    [TenDN] varchar(50) NOT NULL,
    [TokenHash] varchar(64) NOT NULL,
    [NgayTao] datetime2 NOT NULL,
    [HanDung] datetime2 NOT NULL,
    [NgaySuDung] datetime2 NULL,
    [DiaChiIP] varchar(45) NULL,
    [MaXacNhanHash] varchar(64) NULL,
    [SoLanNhapMa] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_YeuCauDatLaiMatKhaus] PRIMARY KEY ([MaYeuCau]),
    CONSTRAINT [FK_YeuCauDatLaiMatKhaus_TaiKhoans_TenDN] FOREIGN KEY ([TenDN]) REFERENCES [TaiKhoans] ([TenDN]) ON DELETE CASCADE
);
GO

CREATE TABLE [PhieuDoiPhongs] (
    [MaPhieuDoi] varchar(15) NOT NULL,
    [MaPhieuNhan] varchar(15) NOT NULL,
    [MaPhongCu] varchar(10) NOT NULL,
    [MaPhongMoi] varchar(10) NOT NULL,
    [NgayDoi] datetime2 NOT NULL,
    [ChenhLechGia] decimal(18,2) NOT NULL,
    [GhiChu] nvarchar(255) NULL,
    CONSTRAINT [PK_PhieuDoiPhongs] PRIMARY KEY ([MaPhieuDoi]),
    CONSTRAINT [FK_PhieuDoiPhongs_ChiTietPhieuNhans_MaPhieuNhan_MaPhongCu] FOREIGN KEY ([MaPhieuNhan], [MaPhongCu]) REFERENCES [ChiTietPhieuNhans] ([MaPhieuNhan], [MaPhong]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PhieuDoiPhongs_Phongs_MaPhongMoi] FOREIGN KEY ([MaPhongMoi]) REFERENCES [Phongs] ([MaPhong]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChiTietHoaDons] (
    [MaChiTietHoaDon] int NOT NULL IDENTITY,
    [MaHD] varchar(15) NOT NULL,
    [LoaiKhoanMuc] nvarchar(20) NOT NULL,
    [MoTa] nvarchar(255) NOT NULL,
    [SoLuong] int NOT NULL DEFAULT 1,
    [DonGia] decimal(18,2) NOT NULL,
    [ThanhTien] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ChiTietHoaDons] PRIMARY KEY ([MaChiTietHoaDon]),
    CONSTRAINT [FK_ChiTietHoaDons_HoaDons_MaHD] FOREIGN KEY ([MaHD]) REFERENCES [HoaDons] ([MaHD]) ON DELETE CASCADE
);
GO

CREATE TABLE [ChiTietThanhToans] (
    [MaThanhToan] int NOT NULL IDENTITY,
    [MaHD] varchar(15) NOT NULL,
    [HinhThucThanhToan] nvarchar(20) NOT NULL,
    [SoTien] decimal(18,2) NOT NULL,
    [ThoiGianThanhToan] datetime2 NOT NULL,
    [MaTaiKhoanThuNgan] varchar(10) NOT NULL,
    [MaGiaoDich] varchar(100) NULL,
    CONSTRAINT [PK_ChiTietThanhToans] PRIMARY KEY ([MaThanhToan]),
    CONSTRAINT [FK_ChiTietThanhToans_HoaDons_MaHD] FOREIGN KEY ([MaHD]) REFERENCES [HoaDons] ([MaHD]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChiTietThanhToans_NhanViens_MaTaiKhoanThuNgan] FOREIGN KEY ([MaTaiKhoanThuNgan]) REFERENCES [NhanViens] ([MaNV]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ThongBaoNganHangs] (
    [MaThongBao] int NOT NULL IDENTITY,
    [MaGiaoDichNganHang] varchar(100) NOT NULL,
    [SoTaiKhoanNhan] varchar(30) NOT NULL,
    [SoTien] decimal(18,2) NOT NULL,
    [NoiDung] nvarchar(255) NOT NULL,
    [ThoiGianNganHang] datetime2 NOT NULL,
    [ThoiGianNhan] datetime2 NOT NULL,
    [KetQua] nvarchar(40) NOT NULL,
    [MaGiaoDich] varchar(15) NULL,
    [GhiChu] nvarchar(255) NULL,
    [TrangThaiXuLy] varchar(20) NOT NULL DEFAULT 'KHONG_CAN',
    [NguoiXuLy] varchar(50) NULL,
    [NgayXuLy] datetime2 NULL,
    [HuongXuLy] varchar(20) NULL,
    [GhiChuXuLy] nvarchar(500) NULL,
    CONSTRAINT [PK_ThongBaoNganHangs] PRIMARY KEY ([MaThongBao]),
    CONSTRAINT [FK_ThongBaoNganHangs_GiaoDichChuyenKhoans_MaGiaoDich] FOREIGN KEY ([MaGiaoDich]) REFERENCES [GiaoDichChuyenKhoans] ([MaGiaoDich]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_ChiTietDichVus_MaDV_TieuDe] ON [ChiTietDichVus] ([MaDV], [TieuDe]);
GO

CREATE INDEX [IX_ChiTietHoaDons_MaHD] ON [ChiTietHoaDons] ([MaHD]);
GO

CREATE INDEX [IX_ChiTietKhuyenMais_MaKM] ON [ChiTietKhuyenMais] ([MaKM]);
GO

CREATE INDEX [IX_ChiTietPhieuDats_MaPhong] ON [ChiTietPhieuDats] ([MaPhong]);
GO

CREATE INDEX [IX_ChiTietPhieuNhans_MaPhong] ON [ChiTietPhieuNhans] ([MaPhong]);
GO

CREATE INDEX [IX_ChiTietSuDungDVs_MaDV] ON [ChiTietSuDungDVs] ([MaDV]);
GO

CREATE INDEX [IX_ChiTietSuDungDVs_MaPhieuNhan] ON [ChiTietSuDungDVs] ([MaPhieuNhan]);
GO

CREATE INDEX [IX_ChiTietThanhToans_MaHD] ON [ChiTietThanhToans] ([MaHD]);
GO

CREATE INDEX [IX_ChiTietThanhToans_MaTaiKhoanThuNgan] ON [ChiTietThanhToans] ([MaTaiKhoanThuNgan]);
GO

CREATE INDEX [IX_CoSoVatChats_MaPhong] ON [CoSoVatChats] ([MaPhong]);
GO

CREATE UNIQUE INDEX [IX_DichVus_TenDV] ON [DichVus] ([TenDV]);
GO

CREATE INDEX [IX_GiaoDichChuyenKhoans_LoaiThamChieu_MaThamChieu_TrangThai] ON [GiaoDichChuyenKhoans] ([LoaiThamChieu], [MaThamChieu], [TrangThai]);
GO

CREATE INDEX [IX_GiaoDichChuyenKhoans_MaNVThuNgan] ON [GiaoDichChuyenKhoans] ([MaNVThuNgan]);
GO

CREATE INDEX [IX_GiaoDichChuyenKhoans_MaTaiKhoanTao] ON [GiaoDichChuyenKhoans] ([MaTaiKhoanTao]);
GO

CREATE UNIQUE INDEX [IX_GiaoDichChuyenKhoans_NoiDungChuyenKhoan] ON [GiaoDichChuyenKhoans] ([NoiDungChuyenKhoan]);
GO

CREATE INDEX [IX_HoaDons_MaNV] ON [HoaDons] ([MaNV]);
GO

CREATE INDEX [IX_HoaDons_MaPhieuNhan] ON [HoaDons] ([MaPhieuNhan]);
GO

CREATE INDEX [IX_KhachDiCungs_MaPhieuDat] ON [KhachDiCungs] ([MaPhieuDat]);
GO

CREATE UNIQUE INDEX [IX_Khachs_CCCD] ON [Khachs] ([CCCD]) WHERE [CCCD] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_Khachs_SoDT] ON [Khachs] ([SoDT]);
GO

CREATE UNIQUE INDEX [IX_KhuyenMais_TenKM] ON [KhuyenMais] ([TenKM]);
GO

CREATE INDEX [IX_LichSuSaoLuus_MaTaiKhoan] ON [LichSuSaoLuus] ([MaTaiKhoan]);
GO

CREATE INDEX [IX_LichSuTinhTrangPhongs_MaPhong_ThoiGian] ON [LichSuTinhTrangPhongs] ([MaPhong], [ThoiGian]);
GO

CREATE INDEX [IX_LichSuTinhTrangPhongs_ThoiGian] ON [LichSuTinhTrangPhongs] ([ThoiGian]);
GO

CREATE UNIQUE INDEX [IX_LoaiPhongs_TenLoai] ON [LoaiPhongs] ([TenLoai]);
GO

CREATE INDEX [IX_NhanViens_MaVaiTro] ON [NhanViens] ([MaVaiTro]);
GO

CREATE INDEX [IX_NhatKyThaoTac_DoiTuong] ON [NhatKyThaoTacs] ([DoiTuongTacDong]);
GO

CREATE INDEX [IX_NhatKyThaoTac_HanhDong] ON [NhatKyThaoTacs] ([HanhDong]);
GO

CREATE INDEX [IX_NhatKyThaoTac_KetQua_ThoiGian] ON [NhatKyThaoTacs] ([KetQua], [ThoiGian]);
GO

CREATE INDEX [IX_NhatKyThaoTac_TaiKhoan_ThoiGian] ON [NhatKyThaoTacs] ([MaTaiKhoan], [ThoiGian]);
GO

CREATE INDEX [IX_NhatKyThaoTac_ThoiGian] ON [NhatKyThaoTacs] ([ThoiGian] DESC, [MaNhatKy] DESC);
GO

CREATE INDEX [IX_PhieuDatPhongs_MaKhach] ON [PhieuDatPhongs] ([MaKhach]);
GO

CREATE INDEX [IX_PhieuDoiPhongs_MaPhieuNhan_MaPhongCu] ON [PhieuDoiPhongs] ([MaPhieuNhan], [MaPhongCu]);
GO

CREATE INDEX [IX_PhieuDoiPhongs_MaPhongMoi] ON [PhieuDoiPhongs] ([MaPhongMoi]);
GO

CREATE INDEX [IX_PhieuNhanPhongs_MaKhach] ON [PhieuNhanPhongs] ([MaKhach]);
GO

CREATE UNIQUE INDEX [IX_PhieuNhanPhongs_MaPhieuDat] ON [PhieuNhanPhongs] ([MaPhieuDat]) WHERE [MaPhieuDat] IS NOT NULL;
GO

CREATE INDEX [IX_Phongs_MaLoai] ON [Phongs] ([MaLoai]);
GO

CREATE UNIQUE INDEX [IX_Quyens_TenQuyen] ON [Quyens] ([TenQuyen]);
GO

CREATE UNIQUE INDEX [IX_TaiKhoans_MaKhach] ON [TaiKhoans] ([MaKhach]) WHERE [MaKhach] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_TaiKhoans_MaNV] ON [TaiKhoans] ([MaNV]) WHERE [MaNV] IS NOT NULL;
GO

CREATE INDEX [IX_ThongBaoNganHangs_MaGiaoDich] ON [ThongBaoNganHangs] ([MaGiaoDich]);
GO

CREATE UNIQUE INDEX [IX_ThongBaoNganHangs_MaGiaoDichNganHang] ON [ThongBaoNganHangs] ([MaGiaoDichNganHang]);
GO

CREATE INDEX [IX_ThongBaoNganHangs_TrangThaiXuLy] ON [ThongBaoNganHangs] ([TrangThaiXuLy]);
GO

CREATE INDEX [IX_VaiTro_Quyen_MaQuyen] ON [VaiTro_Quyen] ([MaQuyen]);
GO

CREATE UNIQUE INDEX [IX_VaiTros_TenVaiTro] ON [VaiTros] ([TenVaiTro]);
GO

CREATE INDEX [IX_YeuCauDatLaiMatKhaus_TenDN_NgayTao] ON [YeuCauDatLaiMatKhaus] ([TenDN], [NgayTao]);
GO

CREATE UNIQUE INDEX [IX_YeuCauDatLaiMatKhaus_TokenHash] ON [YeuCauDatLaiMatKhaus] ([TokenHash]);
GO

-- ------------------------------------------------------------------------------------------------
-- Mô tả bảng (hiển thị ở SSMS: Properties -> Extended Properties -> MS_Description)
-- ------------------------------------------------------------------------------------------------
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Vai trò của hệ thống: QUAN_LY, LE_TAN, KHACH_HANG.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'VaiTros';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Danh mục quyền (mỗi thao tác của API gắn với đúng một quyền).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Quyens';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Quyền được cấp cho từng vai trò (Quản lý chỉnh ở màn Phân quyền).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'VaiTro_Quyen';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Nhân viên khách sạn (Quản lý, Lễ tân).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'NhanViens';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Tài khoản đăng nhập của nhân viên (Desktop) và khách hàng (Website); mật khẩu băm BCrypt.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'TaiKhoans';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Hồ sơ khách thuê phòng (khách đặt qua Website, khách đặt tại quầy và khách vãng lai).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Khachs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Danh mục loại phòng: đơn giá, số khách tiêu chuẩn, phụ thu, tiện nghi.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LoaiPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Danh mục phòng và tình trạng hiện tại (VC, VD, VI, OC, OD, OOO).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Phongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Lịch sử đổi tình trạng phòng (ai đổi, lúc nào, lý do).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LichSuTinhTrangPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Cơ sở vật chất / thiết bị trong phòng hoặc trong kho.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'CoSoVatChats';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Phiếu đặt phòng (cá nhân hoặc đoàn): ngày đón/trả, tiền cọc, trạng thái, thông tin hủy.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PhieuDatPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Các phòng được giữ trong một phiếu đặt phòng.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietPhieuDats';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Giá chốt từng đêm của một phòng trong phiếu đặt (không đổi khi quản lý đổi đơn giá loại phòng sau đó).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'GiaDemDatPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Người ở cùng khai báo theo phiếu đặt phòng.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'KhachDiCungs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Phiếu nhận phòng (theo phiếu đặt hoặc khách vãng lai).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PhieuNhanPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Lượt lưu trú của từng phòng trong một phiếu nhận: ngày nhận/trả, số người, đơn giá, phụ thu.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietPhieuNhans';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Phiếu đổi phòng cho khách đang ở.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PhieuDoiPhongs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Danh mục dịch vụ bán kèm (ăn sáng, giặt ủi, thuê xe...).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'DichVus';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Mô tả chi tiết hiển thị cho khách của từng dịch vụ.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietDichVus';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Dịch vụ khách đã sử dụng trong lượt lưu trú (tên và đơn giá chốt lúc ghi).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietSuDungDVs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Chương trình khuyến mãi (giảm %, giảm số tiền, tặng quà).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'KhuyenMais';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Loại phòng áp dụng của từng khuyến mãi / khuyến mãi áp dụng trên phiếu đặt.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietKhuyenMais';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Hóa đơn lập khi trả phòng cuối cùng của phiếu nhận.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'HoaDons';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Các dòng chi tiết của hóa đơn (phòng, dịch vụ, phụ thu, khuyến mãi).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietHoaDons';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Các lần thanh toán của hóa đơn (tiền mặt, chuyển khoản).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ChiTietThanhToans';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Yêu cầu thanh toán chuyển khoản qua mã QR (đặt cọc hoặc hóa đơn).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'GiaoDichChuyenKhoans';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Nhật ký thông báo tiền về từ ngân hàng (webhook) và kết quả đối chiếu.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ThongBaoNganHangs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Nhật ký thao tác nghiệp vụ (ai, lúc nào, làm gì, đối tượng nào).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'NhatKyThaoTacs';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Lịch sử sao lưu / phục hồi cơ sở dữ liệu.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LichSuSaoLuus';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Mã xác nhận quên mật khẩu (chỉ lưu băm, có thời hạn).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'YeuCauDatLaiMatKhaus';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Câu hỏi thường gặp cho chatbot (từ khóa và câu trả lời).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'CauHoiThuongGaps';
GO
