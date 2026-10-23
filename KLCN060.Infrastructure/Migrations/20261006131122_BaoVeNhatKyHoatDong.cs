using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BaoVeNhatKyHoatDong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTacs_MaTaiKhoan",
                table: "NhatKyThaoTacs");

            migrationBuilder.AddColumn<string>(
                name: "ChuKy",
                table: "NhatKyThaoTacs",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KetQua",
                table: "NhatKyThaoTacs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "THANH_CONG");

            migrationBuilder.AddColumn<string>(
                name: "MaLoi",
                table: "NhatKyThaoTacs",
                type: "varchar(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_DoiTuong",
                table: "NhatKyThaoTacs",
                column: "DoiTuongTacDong");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_HanhDong",
                table: "NhatKyThaoTacs",
                column: "HanhDong");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_KetQua_ThoiGian",
                table: "NhatKyThaoTacs",
                columns: new[] { "KetQua", "ThoiGian" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_TaiKhoan_ThoiGian",
                table: "NhatKyThaoTacs",
                columns: new[] { "MaTaiKhoan", "ThoiGian" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs",
                column: "ThoiGian");
            // Nhat ky chi duoc them moi: chan UPDATE / DELETE o muc CSDL (ke ca khi goi truc tiep bang SQL).
            migrationBuilder.Sql("CREATE TRIGGER TR_NhatKyThaoTac_ChiThem ON NhatKyThaoTacs INSTEAD OF UPDATE, DELETE AS BEGIN SET NOCOUNT ON; RAISERROR(N'Nhat ky hoat dong chi duoc them moi, khong duoc sua hoac xoa.', 16, 1); END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_NhatKyThaoTac_ChiThem");
            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_DoiTuong",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_HanhDong",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_KetQua_ThoiGian",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_TaiKhoan_ThoiGian",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropColumn(
                name: "ChuKy",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropColumn(
                name: "KetQua",
                table: "NhatKyThaoTacs");

            migrationBuilder.DropColumn(
                name: "MaLoi",
                table: "NhatKyThaoTacs");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTacs_MaTaiKhoan",
                table: "NhatKyThaoTacs",
                column: "MaTaiKhoan");
        }
    }
}
