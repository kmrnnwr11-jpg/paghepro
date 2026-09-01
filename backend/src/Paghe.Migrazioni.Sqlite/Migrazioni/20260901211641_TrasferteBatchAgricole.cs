using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class TrasferteBatchAgricole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrasferteJson",
                table: "CedoliniAgricoli",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrasferteJson",
                table: "CedoliniAgricoli");
        }
    }
}
