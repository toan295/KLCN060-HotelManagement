using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChiMucDuyNhatTenVaLienHe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dữ liệu cũ có thể đã trùng: bản sau được thêm hậu tố " (2)", " (3)"... để tạo được chỉ mục duy nhất.
            migrationBuilder.Sql(@"
                ;WITH d AS (SELECT [MaLoai] k, [TenLoai] v, ROW_NUMBER() OVER (PARTITION BY [TenLoai] ORDER BY [MaLoai]) rn FROM [LoaiPhongs])
                UPDATE t SET [TenLoai] = LEFT(t.[TenLoai], 50-LEN(' (' + CAST(d.rn AS varchar(10)) + ')')) + ' (' + CAST(d.rn AS varchar(10)) + ')'
                FROM [LoaiPhongs] t JOIN d ON d.k = t.[MaLoai] WHERE d.rn > 1;");

            // Dữ liệu cũ có thể đã trùng: bản sau được thêm hậu tố " (2)", " (3)"... để tạo được chỉ mục duy nhất.
            migrationBuilder.Sql(@"
                ;WITH d AS (SELECT [MaDV] k, [TenDV] v, ROW_NUMBER() OVER (PARTITION BY [TenDV] ORDER BY [MaDV]) rn FROM [DichVus])
                UPDATE t SET [TenDV] = LEFT(t.[TenDV], 100-LEN(' (' + CAST(d.rn AS varchar(10)) + ')')) + ' (' + CAST(d.rn AS varchar(10)) + ')'
                FROM [DichVus] t JOIN d ON d.k = t.[MaDV] WHERE d.rn > 1;");

            // Dữ liệu cũ có thể đã trùng: bản sau được thêm hậu tố " (2)", " (3)"... để tạo được chỉ mục duy nhất.
            migrationBuilder.Sql(@"
                ;WITH d AS (SELECT [MaKM] k, [TenKM] v, ROW_NUMBER() OVER (PARTITION BY [TenKM] ORDER BY [MaKM]) rn FROM [KhuyenMais])
                UPDATE t SET [TenKM] = LEFT(t.[TenKM], 100-LEN(' (' + CAST(d.rn AS varchar(10)) + ')')) + ' (' + CAST(d.rn AS varchar(10)) + ')'
                FROM [KhuyenMais] t JOIN d ON d.k = t.[MaKM] WHERE d.rn > 1;");

            // CCCD trùng: giữ cho hồ sơ đầu tiên, các hồ sơ sau bỏ trống (SĐT trùng không tự sửa được - cần xử lý tay trước khi chạy).
            migrationBuilder.Sql(@"
                ;WITH d AS (SELECT MaKhach k, ROW_NUMBER() OVER (PARTITION BY CCCD ORDER BY MaKhach) rn FROM Khachs WHERE CCCD IS NOT NULL)
                UPDATE t SET CCCD = NULL FROM Khachs t JOIN d ON d.k = t.MaKhach WHERE d.rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_LoaiPhongs_TenLoai",
                table: "LoaiPhongs",
                column: "TenLoai",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KhuyenMais_TenKM",
                table: "KhuyenMais",
                column: "TenKM",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Khachs_CCCD",
                table: "Khachs",
                column: "CCCD",
                unique: true,
                filter: "[CCCD] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Khachs_SoDT",
                table: "Khachs",
                column: "SoDT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DichVus_TenDV",
                table: "DichVus",
                column: "TenDV",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoaiPhongs_TenLoai",
                table: "LoaiPhongs");

            migrationBuilder.DropIndex(
                name: "IX_KhuyenMais_TenKM",
                table: "KhuyenMais");

            migrationBuilder.DropIndex(
                name: "IX_Khachs_CCCD",
                table: "Khachs");

            migrationBuilder.DropIndex(
                name: "IX_Khachs_SoDT",
                table: "Khachs");

            migrationBuilder.DropIndex(
                name: "IX_DichVus_TenDV",
                table: "DichVus");
        }
    }
}
