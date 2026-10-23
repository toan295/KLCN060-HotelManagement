using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LuuTenDichVuLucBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DonViTinh",
                table: "ChiTietSuDungDVs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenDichVu",
                table: "ChiTietSuDungDVs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Dữ liệu cũ: chốt tên/đơn vị theo danh mục hiện tại để từ nay đổi tên dịch vụ không làm đổi hóa đơn cũ.
            migrationBuilder.Sql("UPDATE u SET u.TenDichVu = d.TenDV, u.DonViTinh = d.DonViTinh FROM ChiTietSuDungDVs u JOIN DichVus d ON d.MaDV = u.MaDV WHERE u.TenDichVu IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DonViTinh",
                table: "ChiTietSuDungDVs");

            migrationBuilder.DropColumn(
                name: "TenDichVu",
                table: "ChiTietSuDungDVs");
        }
    }
}
