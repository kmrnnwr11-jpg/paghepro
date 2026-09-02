using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class ValidazioneBustaAgricola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ValidataDa",
                table: "CedoliniAgricoli",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidataIl",
                table: "CedoliniAgricoli",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidataDa",
                table: "CedoliniAgricoli");

            migrationBuilder.DropColumn(
                name: "ValidataIl",
                table: "CedoliniAgricoli");
        }
    }
}
