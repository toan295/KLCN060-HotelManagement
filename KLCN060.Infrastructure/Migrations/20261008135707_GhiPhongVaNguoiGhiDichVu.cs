using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GhiPhongVaNguoiGhiDichVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaPhong",
                table: "ChiTietSuDungDVs",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiGhi",
                table: "ChiTietSuDungDVs",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            // Dòng dịch vụ cũ: phiếu chỉ có đúng một phòng thì biết chắc dòng đó của phòng nào (phiếu nhiều phòng để trống - không đoán).
            migrationBuilder.Sql(@"UPDATE d SET MaPhong = x.MaPhong
FROM ChiTietSuDungDVs d
CROSS APPLY (SELECT MIN(c.MaPhong) AS MaPhong, COUNT(*) AS Dem FROM ChiTietPhieuNhans c WHERE c.MaPhieuNhan = d.MaPhieuNhan) x
WHERE x.Dem = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaPhong",
                table: "ChiTietSuDungDVs");

            migrationBuilder.DropColumn(
                name: "NguoiGhi",
                table: "ChiTietSuDungDVs");
        }
    }
}
