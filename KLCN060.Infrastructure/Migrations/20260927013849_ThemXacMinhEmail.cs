using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemXacMinhEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailDaXacMinh",
                table: "Khachs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayXacMinhEmail",
                table: "Khachs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "YeuCauXacMinhEmails",
                columns: table => new
                {
                    MaYeuCau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaKhach = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanDung = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NgaySuDung = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauXacMinhEmails", x => x.MaYeuCau);
                    table.ForeignKey(
                        name: "FK_YeuCauXacMinhEmails_Khachs_MaKhach",
                        column: x => x.MaKhach,
                        principalTable: "Khachs",
                        principalColumn: "MaKhach",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauXacMinhEmails_MaKhach_NgayTao",
                table: "YeuCauXacMinhEmails",
                columns: new[] { "MaKhach", "NgayTao" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauXacMinhEmails_TokenHash",
                table: "YeuCauXacMinhEmails",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauXacMinhEmails");

            migrationBuilder.DropColumn(
                name: "EmailDaXacMinh",
                table: "Khachs");

            migrationBuilder.DropColumn(
                name: "NgayXacMinhEmail",
                table: "Khachs");
        }
    }
}
