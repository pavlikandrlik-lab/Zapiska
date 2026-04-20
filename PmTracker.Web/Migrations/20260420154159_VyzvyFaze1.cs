using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PmTracker.Web.Migrations
{
    /// <inheritdoc />
    public partial class VyzvyFaze1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ciselnik_vyzvy");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameColumn(
                name: "vyzva",
                table: "zaznam_externi_odkazy",
                newName: "vyzva_id");

            migrationBuilder.AlterColumn<string>(
                name: "cislo",
                table: "zaznam_externi_odkazy",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "zaradid_do_vyzvy",
                table: "zaznam_externi_odkazy",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "cislo_ramcove_smlouvy",
                table: "projekty",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "misto_plneni",
                table: "projekty",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "poradi",
                table: "projekt_subsystemy",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "search_reindex_checkpoint",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    last_processed_audit_id = table.Column<long>(type: "bigint", nullable: false),
                    last_processed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_reindex_checkpoint", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vyzvy",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    projekt_id = table.Column<int>(type: "int", nullable: false),
                    kod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    poradove_v_roce = table.Column<int>(type: "int", nullable: false),
                    rok = table.Column<int>(type: "int", nullable: false),
                    stav = table.Column<byte>(type: "tinyint", nullable: false),
                    datum_zalozeni = table.Column<DateTime>(type: "datetime2", nullable: false),
                    zalozil_osoba_id = table.Column<int>(type: "int", nullable: false),
                    datum_odeslani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    odeslal_osoba_id = table.Column<int>(type: "int", nullable: true),
                    misto_plneni_snapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    cislo_ramcove_smlouvy_snapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vyzvy", x => x.id);
                    table.ForeignKey(
                        name: "FK_vyzvy_projekty_projekt_id",
                        column: x => x.projekt_id,
                        principalTable: "projekty",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zaznam_navrhy",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    projekt_id = table.Column<int>(type: "int", nullable: false),
                    zaznam_id = table.Column<int>(type: "int", nullable: true),
                    subsystem_id = table.Column<int>(type: "int", nullable: false),
                    typ_navrhu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    stav = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_by_osoba_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    decided_by_osoba_id = table.Column<int>(type: "int", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_record_id = table.Column<int>(type: "int", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zaznam_navrhy", x => x.id);
                    table.CheckConstraint("CK_zaznam_navrhy_stav", "stav IN ('PENDING', 'APPROVED', 'REJECTED')");
                    table.CheckConstraint("CK_zaznam_navrhy_typ", "typ_navrhu IN ('CREATE_RECORD', 'SCHEDULE_PLAN_CHANGE')");
                });

            migrationBuilder.CreateTable(
                name: "zaznam_priority_rebuild_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    last_full_rebuild_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_full_rebuild_status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    last_full_rebuild_duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    last_full_rebuild_task_count = table.Column<int>(type: "int", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zaznam_priority_rebuild_state", x => x.id);
                    table.CheckConstraint("CK_zaznam_priority_rebuild_state_singleton", "id = 1");
                    table.CheckConstraint("CK_zaznam_priority_rebuild_state_status", "last_full_rebuild_status IN ('NEVER', 'SUCCESS', 'FAILED', 'RUNNING')");
                });

            migrationBuilder.CreateTable(
                name: "zaznam_priority_uzivatelu",
                columns: table => new
                {
                    zaznam_id = table.Column<int>(type: "int", nullable: false),
                    osoba_id = table.Column<int>(type: "int", nullable: false),
                    score = table.Column<int>(type: "int", nullable: false),
                    computed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    role_weight = table.Column<int>(type: "int", nullable: false),
                    deadline_signal = table.Column<int>(type: "int", nullable: false),
                    milestone_signal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zaznam_priority_uzivatelu", x => new { x.zaznam_id, x.osoba_id });
                });

            migrationBuilder.CreateTable(
                name: "vyzva_historie_stavu",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vyzva_id = table.Column<int>(type: "int", nullable: false),
                    puvodni_stav = table.Column<byte>(type: "tinyint", nullable: true),
                    novy_stav = table.Column<byte>(type: "tinyint", nullable: false),
                    datum_zmeny = table.Column<DateTime>(type: "datetime2", nullable: false),
                    zmenil_osoba_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vyzva_historie_stavu", x => x.id);
                    table.ForeignKey(
                        name: "FK_vyzva_historie_stavu_vyzvy_vyzva_id",
                        column: x => x.vyzva_id,
                        principalTable: "vyzvy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_zaznam_externi_odkazy_vyzva_id",
                table: "zaznam_externi_odkazy",
                column: "vyzva_id");

            migrationBuilder.CreateIndex(
                name: "ux_zaznam_externi_odkazy_cislo_in_vyzve",
                table: "zaznam_externi_odkazy",
                column: "cislo",
                unique: true,
                filter: "[vyzva_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_projekt_subsystemy_projekt_poradi_aktivni",
                table: "projekt_subsystemy",
                columns: new[] { "projekt_id", "poradi" },
                unique: true,
                filter: "[datum_odebrani] IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vyzva_historie_stavu_vyzva_id",
                table: "vyzva_historie_stavu",
                column: "vyzva_id");

            migrationBuilder.CreateIndex(
                name: "ix_vyzvy_projekt_datum_desc",
                table: "vyzvy",
                columns: new[] { "projekt_id", "datum_zalozeni" });

            migrationBuilder.CreateIndex(
                name: "ux_vyzvy_smlouva_rok_poradove",
                table: "vyzvy",
                columns: new[] { "cislo_ramcove_smlouvy_snapshot", "rok", "poradove_v_roce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zaznam_navrhy_projekt_created_at",
                table: "zaznam_navrhy",
                columns: new[] { "projekt_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_zaznam_navrhy_zaznam_typ_stav",
                table: "zaznam_navrhy",
                columns: new[] { "zaznam_id", "typ_navrhu", "stav" });

            migrationBuilder.CreateIndex(
                name: "UX_zaznam_navrhy_pending_schedule_per_record",
                table: "zaznam_navrhy",
                columns: new[] { "zaznam_id", "typ_navrhu" },
                unique: true,
                filter: "[stav] = 'PENDING' AND [typ_navrhu] = 'SCHEDULE_PLAN_CHANGE'");

            migrationBuilder.CreateIndex(
                name: "IX_zaznam_priority_uzivatelu_osoba_score_zaznam",
                table: "zaznam_priority_uzivatelu",
                columns: new[] { "osoba_id", "score", "zaznam_id" },
                descending: new[] { false, true, false });

            migrationBuilder.AddForeignKey(
                name: "FK_zaznam_externi_odkazy_vyzvy_vyzva_id",
                table: "zaznam_externi_odkazy",
                column: "vyzva_id",
                principalTable: "vyzvy",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_zaznam_externi_odkazy_vyzvy_vyzva_id",
                table: "zaznam_externi_odkazy");

            migrationBuilder.DropTable(
                name: "search_reindex_checkpoint",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vyzva_historie_stavu");

            migrationBuilder.DropTable(
                name: "zaznam_navrhy");

            migrationBuilder.DropTable(
                name: "zaznam_priority_rebuild_state");

            migrationBuilder.DropTable(
                name: "zaznam_priority_uzivatelu");

            migrationBuilder.DropTable(
                name: "vyzvy");

            migrationBuilder.DropIndex(
                name: "IX_zaznam_externi_odkazy_vyzva_id",
                table: "zaznam_externi_odkazy");

            migrationBuilder.DropIndex(
                name: "ux_zaznam_externi_odkazy_cislo_in_vyzve",
                table: "zaznam_externi_odkazy");

            migrationBuilder.DropIndex(
                name: "UX_projekt_subsystemy_projekt_poradi_aktivni",
                table: "projekt_subsystemy");

            migrationBuilder.DropColumn(
                name: "zaradid_do_vyzvy",
                table: "zaznam_externi_odkazy");

            migrationBuilder.DropColumn(
                name: "cislo_ramcove_smlouvy",
                table: "projekty");

            migrationBuilder.DropColumn(
                name: "misto_plneni",
                table: "projekty");

            migrationBuilder.DropColumn(
                name: "poradi",
                table: "projekt_subsystemy");

            migrationBuilder.RenameColumn(
                name: "vyzva_id",
                table: "zaznam_externi_odkazy",
                newName: "vyzva");

            migrationBuilder.AlterColumn<string>(
                name: "cislo",
                table: "zaznam_externi_odkazy",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "ciselnik_vyzvy",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    is_locked = table.Column<bool>(type: "bit", nullable: false),
                    kod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    nazev = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rok = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ciselnik_vyzvy", x => x.id);
                });
        }
    }
}
