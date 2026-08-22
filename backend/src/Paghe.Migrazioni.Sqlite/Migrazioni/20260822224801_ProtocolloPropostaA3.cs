using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class ProtocolloPropostaA3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EffettiUsati",
                table: "AiScopeGrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxEffetti",
                table: "AiScopeGrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxProposte",
                table: "AiScopeGrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PermessiAmmessi",
                table: "AiScopeGrants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProposteUsate",
                table: "AiScopeGrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SlotA3Attivo",
                table: "AiScopeGrants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StrumentiAmmessi",
                table: "AiScopeGrants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnullataUtc",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AziendaId",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Confermabile",
                table: "AiProposte",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EsitoUndoProtetto",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentoHash",
                table: "AiProposte",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Operazione",
                table: "AiProposte",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Periodo",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostcondizioneHash",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrecondizioneHash",
                table: "AiProposte",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RipristinataUtc",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Risorsa",
                table: "AiProposte",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SlotAttivo",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UndoProtetto",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UndoScadeUtc",
                table: "AiProposte",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersioneStrumento",
                table: "AiProposte",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AiScopeGrants_TenantId_UtenteId_SlotA3Attivo",
                table: "AiScopeGrants",
                columns: new[] { "TenantId", "UtenteId", "SlotA3Attivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProposte_TenantId_GrantId_SlotAttivo",
                table: "AiProposte",
                columns: new[] { "TenantId", "GrantId", "SlotAttivo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiScopeGrants_TenantId_UtenteId_SlotA3Attivo",
                table: "AiScopeGrants");

            migrationBuilder.DropIndex(
                name: "IX_AiProposte_TenantId_GrantId_SlotAttivo",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "EffettiUsati",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "MaxEffetti",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "MaxProposte",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "PermessiAmmessi",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "ProposteUsate",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "SlotA3Attivo",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "StrumentiAmmessi",
                table: "AiScopeGrants");

            migrationBuilder.DropColumn(
                name: "AnnullataUtc",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "AziendaId",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "Confermabile",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "EsitoUndoProtetto",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "IntentoHash",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "Operazione",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "Periodo",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "PostcondizioneHash",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "PrecondizioneHash",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "RipristinataUtc",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "Risorsa",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "SlotAttivo",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "UndoProtetto",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "UndoScadeUtc",
                table: "AiProposte");

            migrationBuilder.DropColumn(
                name: "VersioneStrumento",
                table: "AiProposte");
        }
    }
}
