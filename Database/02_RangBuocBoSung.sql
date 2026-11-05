/* =====================================================================================================
   Script 02: Ràng buộc CHECK bổ sung (dữ liệu hợp lệ ngay ở tầng cơ sở dữ liệu, không chỉ ở tầng API).
   Chạy sau script 01 và trước khi nạp dữ liệu.
   ===================================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
USE [KLCN060_QuanLyKhachSan];
GO

IF OBJECT_ID(N'dbo.CK_LoaiPhongs_DonGia', N'C') IS NULL
    ALTER TABLE [dbo].[LoaiPhongs] ADD CONSTRAINT [CK_LoaiPhongs_DonGia] CHECK ([DonGia] >= 0 AND [PhuThu] >= 0 AND [SoNguoiTieuChuan] > 0);
GO

IF OBJECT_ID(N'dbo.CK_DichVus_GiaDV', N'C') IS NULL
    ALTER TABLE [dbo].[DichVus] ADD CONSTRAINT [CK_DichVus_GiaDV] CHECK ([GiaDV] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_KhuyenMais_Ngay', N'C') IS NULL
    ALTER TABLE [dbo].[KhuyenMais] ADD CONSTRAINT [CK_KhuyenMais_Ngay] CHECK ([NgayKetThuc] >= [NgayBatDau]);
GO

IF OBJECT_ID(N'dbo.CK_KhuyenMais_PhanTram', N'C') IS NULL
    ALTER TABLE [dbo].[KhuyenMais] ADD CONSTRAINT [CK_KhuyenMais_PhanTram] CHECK ([PhanTramKM] IS NULL OR ([PhanTramKM] >= 0 AND [PhanTramKM] <= 100));
GO

IF OBJECT_ID(N'dbo.CK_PhieuDatPhongs_Ngay', N'C') IS NULL
    ALTER TABLE [dbo].[PhieuDatPhongs] ADD CONSTRAINT [CK_PhieuDatPhongs_Ngay] CHECK ([NgayTraDuKien] > [NgayDonDuKien]);
GO

IF OBJECT_ID(N'dbo.CK_PhieuDatPhongs_Tien', N'C') IS NULL
    ALTER TABLE [dbo].[PhieuDatPhongs] ADD CONSTRAINT [CK_PhieuDatPhongs_Tien] CHECK ([TienCoc] >= 0 AND [SoTienHoanCoc] >= 0 AND [TienCocGiuLai] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_ChiTietPhieuDats_SoLuong', N'C') IS NULL
    ALTER TABLE [dbo].[ChiTietPhieuDats] ADD CONSTRAINT [CK_ChiTietPhieuDats_SoLuong] CHECK ([SoLuong] > 0 AND [DonGiaApDung] >= 0 AND [ThanhTien] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_ChiTietPhieuNhans_SoNguoi', N'C') IS NULL
    ALTER TABLE [dbo].[ChiTietPhieuNhans] ADD CONSTRAINT [CK_ChiTietPhieuNhans_SoNguoi] CHECK ([SoNguoi] > 0 AND [DonGia] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_ChiTietSuDungDVs_SoLuong', N'C') IS NULL
    ALTER TABLE [dbo].[ChiTietSuDungDVs] ADD CONSTRAINT [CK_ChiTietSuDungDVs_SoLuong] CHECK ([SoLuong] > 0 AND [ThanhTien] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_HoaDons_Tien', N'C') IS NULL
    ALTER TABLE [dbo].[HoaDons] ADD CONSTRAINT [CK_HoaDons_Tien] CHECK ([TongTien] >= 0 AND [TienPhong] >= 0 AND [TienDV] >= 0);
GO

IF OBJECT_ID(N'dbo.CK_ChiTietThanhToans_SoTien', N'C') IS NULL
    ALTER TABLE [dbo].[ChiTietThanhToans] ADD CONSTRAINT [CK_ChiTietThanhToans_SoTien] CHECK ([SoTien] > 0);
GO
