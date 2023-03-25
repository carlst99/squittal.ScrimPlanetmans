using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace squittal.ScrimPlanetmans.App.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsForScrimMatchReportInfantryPlayerClassEventCountsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "RevivedLoadoutId",
                table: "ScrimRevive",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "MedicLoadoutId",
                table: "ScrimRevive",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrestigeLevel",
                table: "ScrimMatchParticipatingPlayer",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "AttackerLoadoutId",
                table: "ScrimDamageAssist",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrestigeLevel",
                table: "ScrimMatchParticipatingPlayer");

            migrationBuilder.DropColumn(
                name: "AttackerLoadoutId",
                table: "ScrimDamageAssist");

            migrationBuilder.AlterColumn<int>(
                name: "RevivedLoadoutId",
                table: "ScrimRevive",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MedicLoadoutId",
                table: "ScrimRevive",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
