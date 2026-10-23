using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DinhMucDoDungTheoPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CachTinh",
                table: "VatDungs",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "MOI_PHONG");

            migrationBuilder.CreateTable(
                name: "VatDungLoaiPhongs",
                columns: table => new
                {
                    MaVatDung = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    MaLoai = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatDungLoaiPhongs", x => new { x.MaVatDung, x.MaLoai });
                    table.ForeignKey(
                        name: "FK_VatDungLoaiPhongs_LoaiPhongs_MaLoai",
                        column: x => x.MaLoai,
                        principalTable: "LoaiPhongs",
                        principalColumn: "MaLoai",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VatDungLoaiPhongs_VatDungs_MaVatDung",
                        column: x => x.MaVatDung,
                        principalTable: "VatDungs",
                        principalColumn: "MaVatDung",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VatDungLoaiPhongs_MaLoai",
                table: "VatDungLoaiPhongs",
                column: "MaLoai");
            // Nâng cấp định mức của đồ dùng mẫu theo cách tính mới (chỉ khi còn đúng giá trị mẫu ban đầu, không ghi đè chỉnh sửa của quản lý):
            // khăn/amenities/dép/nước suối tính theo người; ga/gối tính theo giường. Phòng 1 giường đôi cho 2 người vẫn ra đúng định mức cũ.
            migrationBuilder.Sql(@"
UPDATE VatDungs SET CachTinh = 'MOI_NGUOI', SoLuongChuan = 1 WHERE MaVatDung IN ('VT01','VT02','VT05','VT08','VT09') AND SoLuongChuan = 2;
UPDATE VatDungs SET CachTinh = 'MOI_GIUONG', SoLuongChuan = 2 WHERE MaVatDung = 'VT04' AND SoLuongChuan = 2;
UPDATE VatDungs SET CachTinh = 'MOI_GIUONG', SoLuongChuan = 1 WHERE MaVatDung = 'VT03' AND SoLuongChuan = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VatDungLoaiPhongs");

            migrationBuilder.DropColumn(
                name: "CachTinh",
                table: "VatDungs");
        }
    }
}
