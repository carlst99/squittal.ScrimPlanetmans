using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace squittal.ScrimPlanetmans.App.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScrimAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrimAction");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrimAction",
                columns: table => new
                {
                    Action = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Domain = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimAction", x => x.Action);
                });
        }
    }
}
