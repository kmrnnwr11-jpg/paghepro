using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Postgres.Migrazioni
{
    /// <inheritdoc />
    public partial class PreferenzeUtente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreferenzeUtenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chiave = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Valore = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggiornatoIl = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenzeUtenti", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreferenzeUtenti_TenantId_UtenteId_Chiave",
                table: "PreferenzeUtenti",
                columns: new[] { "TenantId", "UtenteId", "Chiave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferenzeUtenti");
        }
    }
}
