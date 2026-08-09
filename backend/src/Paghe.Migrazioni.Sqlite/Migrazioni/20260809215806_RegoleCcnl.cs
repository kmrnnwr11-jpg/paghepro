using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class RegoleCcnl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegoleCcnl",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CcnlCodice = table.Column<string>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    DivisoreOrario = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    DivisoreGiornaliero = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    MaggiorazioneStraordinario = table.Column<decimal>(type: "TEXT", precision: 6, scale: 4, nullable: false),
                    MaggiorazioneStraordinarioNotturno = table.Column<decimal>(type: "TEXT", precision: 6, scale: 4, nullable: false),
                    MaggiorazioneStraordinarioFestivo = table.Column<decimal>(type: "TEXT", precision: 6, scale: 4, nullable: false),
                    GiorniFerieMese = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: true),
                    OrePermessiMese = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    Fonte = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegoleCcnl", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegoleCcnl_CcnlCodice_Anno",
                table: "RegoleCcnl",
                columns: new[] { "CcnlCodice", "Anno" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegoleCcnl");
        }
    }
}
