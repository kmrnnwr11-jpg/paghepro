using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class VersioneCartellinoBozza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Versione",
                table: "Cartellini",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Versione",
                table: "Cartellini");
        }
    }
}
