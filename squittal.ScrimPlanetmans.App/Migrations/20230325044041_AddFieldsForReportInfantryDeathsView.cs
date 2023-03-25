using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace squittal.ScrimPlanetmans.App.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsForReportInfantryDeathsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameDisplay",
                table: "ScrimMatchParticipatingPlayer",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExperienceGainId",
                table: "ScrimGrenadeAssist",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AttackerFactionId",
                table: "ScrimDeath",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttackerLoadoutId",
                table: "ScrimDeath",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "VictimFactionId",
                table: "ScrimDeath",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "VictimLoadoutId",
                table: "ScrimDeath",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameDisplay",
                table: "ScrimMatchParticipatingPlayer");

            migrationBuilder.DropColumn(
                name: "ExperienceGainId",
                table: "ScrimGrenadeAssist");

            migrationBuilder.DropColumn(
                name: "AttackerFactionId",
                table: "ScrimDeath");

            migrationBuilder.DropColumn(
                name: "AttackerLoadoutId",
                table: "ScrimDeath");

            migrationBuilder.DropColumn(
                name: "VictimFactionId",
                table: "ScrimDeath");

            migrationBuilder.DropColumn(
                name: "VictimLoadoutId",
                table: "ScrimDeath");
        }
    }
}
