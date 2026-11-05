/* Xóa hoàn toàn cơ sở dữ liệu KLCN060_QuanLyKhachSan (dùng khi muốn tạo lại từ đầu). CẨN THẬN: mất toàn bộ dữ liệu. */
USE [master];
GO
IF DB_ID(N'KLCN060_QuanLyKhachSan') IS NOT NULL
BEGIN
    ALTER DATABASE [KLCN060_QuanLyKhachSan] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [KLCN060_QuanLyKhachSan];
END
GO
