using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace squittal.ScrimPlanetmans.App.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsForScrimDamageAssistDamageDealtView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperienceGainAmount",
                table: "ScrimDamageAssist",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceGainId",
                table: "ScrimDamageAssist",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperienceGainAmount",
                table: "ScrimDamageAssist");

            migrationBuilder.DropColumn(
                name: "ExperienceGainId",
                table: "ScrimDamageAssist");
        }
    }
}
