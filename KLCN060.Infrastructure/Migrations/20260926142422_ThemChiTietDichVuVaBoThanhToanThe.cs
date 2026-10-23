using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemChiTietDichVuVaBoThanhToanThe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ hình thức thanh toán "THE": chuyển các lần thanh toán bằng thẻ đã có sang chuyển khoản (giữ nguyên số tiền,
            // thời điểm, thu ngân) và đổi mã giao dịch POS-... thành mã ngân hàng dạng FT + yyMMdd + số thứ tự (bắt đầu từ 51 để
            // không trùng mã mẫu). Bản ghi đối soát tương ứng do DataSeeder bổ sung ở lần khởi động API kế tiếp.
            migrationBuilder.Sql(@"
;WITH the AS (
    SELECT MaThanhToan, ROW_NUMBER() OVER (ORDER BY MaThanhToan) AS stt
    FROM ChiTietThanhToans WHERE HinhThucThanhToan = 'THE')
UPDATE ct
SET HinhThucThanhToan = 'CHUYEN_KHOAN',
    MaGiaoDich = 'FT' + FORMAT(ct.ThoiGianThanhToan, 'yyMMdd') + CAST(50 + the.stt AS varchar(10))
FROM ChiTietThanhToans ct JOIN the ON the.MaThanhToan = ct.MaThanhToan;");
            migrationBuilder.Sql("UPDATE HoaDons SET HinhThucThanhToan = 'CHUYEN_KHOAN' WHERE HinhThucThanhToan = 'THE';");

            migrationBuilder.CreateTable(
                name: "ChiTietDichVus",
                columns: table => new
                {
                    MaCTDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaDV = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConHienThi = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDichVus", x => x.MaCTDV);
                    table.ForeignKey(
                        name: "FK_ChiTietDichVus_DichVus_MaDV",
                        column: x => x.MaDV,
                        principalTable: "DichVus",
                        principalColumn: "MaDV",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDichVus_MaDV_TieuDe",
                table: "ChiTietDichVus",
                columns: new[] { "MaDV", "TieuDe" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không khôi phục hình thức "THE" đã chuyển sang chuyển khoản (đã bị loại khỏi hệ thống).
            migrationBuilder.DropTable(
                name: "ChiTietDichVus");
        }
    }
}
