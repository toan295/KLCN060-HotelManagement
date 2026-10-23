using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChotPhuThuNguoiThemTheoLuotO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PhuThuNguoiThemChot",
                table: "ChiTietPhieuNhans",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoNguoiTieuChuanChot",
                table: "ChiTietPhieuNhans",
                type: "int",
                nullable: true);

            // Chốt cho các lượt ở hiện có theo loại phòng hiện hành (từ nay đổi loại phòng không làm đổi hóa đơn đã/đang tính).
            migrationBuilder.Sql(@"
                UPDATE ct SET SoNguoiTieuChuanChot = lp.SoNguoiTieuChuan, PhuThuNguoiThemChot = lp.PhuThu
                FROM ChiTietPhieuNhans ct JOIN Phongs p ON p.MaPhong = ct.MaPhong JOIN LoaiPhongs lp ON lp.MaLoai = p.MaLoai;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhuThuNguoiThemChot",
                table: "ChiTietPhieuNhans");

            migrationBuilder.DropColumn(
                name: "SoNguoiTieuChuanChot",
                table: "ChiTietPhieuNhans");
        }
    }
}
