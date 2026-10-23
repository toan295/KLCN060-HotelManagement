using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemMaKhachVaoPhieuNhanPhong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaKhach",
                table: "PhieuNhanPhongs",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuNhanPhongs_MaKhach",
                table: "PhieuNhanPhongs",
                column: "MaKhach");

            migrationBuilder.AddForeignKey(
                name: "FK_PhieuNhanPhongs_Khachs_MaKhach",
                table: "PhieuNhanPhongs",
                column: "MaKhach",
                principalTable: "Khachs",
                principalColumn: "MaKhach",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhieuNhanPhongs_Khachs_MaKhach",
                table: "PhieuNhanPhongs");

            migrationBuilder.DropIndex(
                name: "IX_PhieuNhanPhongs_MaKhach",
                table: "PhieuNhanPhongs");

            migrationBuilder.DropColumn(
                name: "MaKhach",
                table: "PhieuNhanPhongs");
        }
    }
}
