using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChiMucNhatKyGiamDan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs",
                columns: new[] { "ThoiGian", "MaNhatKy" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyThaoTac_ThoiGian",
                table: "NhatKyThaoTacs",
                column: "ThoiGian");
        }
    }
}
