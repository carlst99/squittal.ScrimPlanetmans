using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace squittal.ScrimPlanetmans.App.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructedTeam",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsHiddenFromSelection = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructedTeam", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ruleset",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateLastModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsCustomDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SourceFile = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: ""),
                    DefaultMatchTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultRoundLength = table.Column<int>(type: "int", nullable: false, defaultValue: 900),
                    DefaultEndRoundOnFacilityCapture = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ruleset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrimAction",
                columns: table => new
                {
                    Action = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Domain = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimAction", x => x.Action);
                });

            migrationBuilder.CreateTable(
                name: "ScrimDamageAssist",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttackerCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    VictimCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    AttackerTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    VictimTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimDamageAssist", x => new { x.ScrimMatchId, x.Timestamp, x.AttackerCharacterId, x.VictimCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimDeath",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VictimCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    AttackerCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false, defaultValue: -1),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    DeathType = table.Column<int>(type: "int", nullable: false),
                    AttackerTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    VictimTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    IsHeadshot = table.Column<bool>(type: "bit", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    WeaponId = table.Column<long>(type: "bigint", nullable: true),
                    AttackerVehicleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimDeath", x => new { x.ScrimMatchId, x.Timestamp, x.AttackerCharacterId, x.VictimCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimFacilityControl",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FacilityId = table.Column<int>(type: "int", nullable: false),
                    ControllingTeamOrdinal = table.Column<int>(type: "int", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ControlType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimFacilityControl", x => new { x.ScrimMatchId, x.Timestamp, x.FacilityId, x.ControllingTeamOrdinal });
                });

            migrationBuilder.CreateTable(
                name: "ScrimGrenadeAssist",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttackerCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    VictimCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AttackerTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    VictimTeamOrdinal = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimGrenadeAssist", x => new { x.ScrimMatchId, x.Timestamp, x.AttackerCharacterId, x.VictimCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimMatchParticipatingPlayer",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    TeamOrdinal = table.Column<int>(type: "int", nullable: false),
                    FactionId = table.Column<int>(type: "int", nullable: false),
                    IsFromOutfit = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OutfitId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    IsFromConstructedTeam = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ConstructedTeamId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimMatchParticipatingPlayer", x => new { x.ScrimMatchId, x.CharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimMatchTeamResult",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamOrdinal = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NetScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Kills = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Deaths = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Headshots = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HeadshotDeaths = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Suicides = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Teamkills = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TeamkillDeaths = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RevivesGiven = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RevivesTaken = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DamageAssists = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UtilityAssists = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DamageAssistedDeaths = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UtilityAssistedDeaths = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ObjectiveCaptureTicks = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ObjectiveDefenseTicks = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    BaseDefenses = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    BaseCaptures = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimMatchTeamResult", x => new { x.ScrimMatchId, x.TeamOrdinal });
                });

            migrationBuilder.CreateTable(
                name: "ScrimRevive",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MedicCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    RevivedCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    MedicTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    RevivedTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    MedicLoadoutId = table.Column<int>(type: "int", nullable: true),
                    RevivedLoadoutId = table.Column<int>(type: "int", nullable: true),
                    ExperienceGainId = table.Column<int>(type: "int", nullable: false),
                    ExperienceGainAmount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ZoneId = table.Column<int>(type: "int", nullable: true),
                    WorldId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimRevive", x => new { x.ScrimMatchId, x.Timestamp, x.MedicCharacterId, x.RevivedCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimSpotAssist",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SpotterCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    VictimCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SpotterTeamOrdinal = table.Column<int>(type: "int", nullable: true),
                    VictimTeamOrdinal = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimSpotAssist", x => new { x.ScrimMatchId, x.Timestamp, x.SpotterCharacterId, x.VictimCharacterId });
                });

            migrationBuilder.CreateTable(
                name: "ScrimVehicleDestruction",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttackerCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    VictimCharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    VictimVehicleId = table.Column<long>(type: "bigint", nullable: false),
                    AttackerVehicleId = table.Column<long>(type: "bigint", nullable: true),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false, defaultValue: -1),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    DeathType = table.Column<int>(type: "int", nullable: false),
                    WeaponId = table.Column<long>(type: "bigint", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimVehicleDestruction", x => new { x.ScrimMatchId, x.Timestamp, x.AttackerCharacterId, x.VictimCharacterId, x.VictimVehicleId });
                });

            migrationBuilder.CreateTable(
                name: "ConstructedTeamPlayerMembership",
                columns: table => new
                {
                    ConstructedTeamId = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    FactionId = table.Column<int>(type: "int", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructedTeamPlayerMembership", x => new { x.ConstructedTeamId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_ConstructedTeamPlayerMembership_ConstructedTeam_ConstructedTeamId",
                        column: x => x.ConstructedTeamId,
                        principalTable: "ConstructedTeam",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulesetActionRule",
                columns: table => new
                {
                    RulesetId = table.Column<int>(type: "int", nullable: false),
                    ScrimActionType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DeferToItemCategoryRules = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ScrimActionTypeDomain = table.Column<int>(type: "int", nullable: false, defaultValue: -1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesetActionRule", x => new { x.RulesetId, x.ScrimActionType });
                    table.ForeignKey(
                        name: "FK_RulesetActionRule_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulesetFacilityRule",
                columns: table => new
                {
                    RulesetId = table.Column<int>(type: "int", nullable: false),
                    FacilityId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesetFacilityRule", x => new { x.RulesetId, x.FacilityId });
                    table.ForeignKey(
                        name: "FK_RulesetFacilityRule_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulesetItemCategoryRule",
                columns: table => new
                {
                    RulesetId = table.Column<int>(type: "int", nullable: false),
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeferToItemRules = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeferToPlanetsideClassSettings = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InfiltratorIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InfiltratorPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LightAssaultIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LightAssaultPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MedicIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MedicPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EngineerIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EngineerPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HeavyAssaultIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HeavyAssaultPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesetItemCategoryRule", x => new { x.RulesetId, x.ItemCategoryId });
                    table.ForeignKey(
                        name: "FK_RulesetItemCategoryRule_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulesetItemRule",
                columns: table => new
                {
                    RulesetId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    ItemCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeferToPlanetsideClassSettings = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InfiltratorIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InfiltratorPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LightAssaultIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LightAssaultPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MedicIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MedicPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EngineerIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EngineerPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HeavyAssaultIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HeavyAssaultPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxIsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesetItemRule", x => new { x.RulesetId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_RulesetItemRule_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulesetOverlayConfiguration",
                columns: table => new
                {
                    RulesetId = table.Column<int>(type: "int", nullable: false),
                    UseCompactLayout = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    StatsDisplayType = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ShowStatusPanelScores = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulesetOverlayConfiguration", x => x.RulesetId);
                    table.ForeignKey(
                        name: "FK_RulesetOverlayConfiguration_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrimMatch",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RulesetId = table.Column<int>(type: "int", nullable: false, defaultValue: -1),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimMatch", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrimMatch_Ruleset_RulesetId",
                        column: x => x.RulesetId,
                        principalTable: "Ruleset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrimMatchTeamPointAdjustment",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamOrdinal = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimMatchTeamPointAdjustment", x => new { x.ScrimMatchId, x.TeamOrdinal, x.Timestamp });
                    table.ForeignKey(
                        name: "FK_ScrimMatchTeamPointAdjustment_ScrimMatchTeamResult_ScrimMatchId_TeamOrdinal",
                        columns: x => new { x.ScrimMatchId, x.TeamOrdinal },
                        principalTable: "ScrimMatchTeamResult",
                        principalColumns: new[] { "ScrimMatchId", "TeamOrdinal" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrimMatchRoundConfiguration",
                columns: table => new
                {
                    ScrimMatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ScrimMatchRound = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoundSecondsTotal = table.Column<int>(type: "int", nullable: false),
                    WorldId = table.Column<int>(type: "int", nullable: false),
                    IsManualWorldId = table.Column<bool>(type: "bit", nullable: false),
                    FacilityId = table.Column<long>(type: "bigint", nullable: true),
                    IsRoundEndedOnFacilityCapture = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrimMatchRoundConfiguration", x => new { x.ScrimMatchId, x.ScrimMatchRound });
                    table.ForeignKey(
                        name: "FK_ScrimMatchRoundConfiguration_ScrimMatch_ScrimMatchId",
                        column: x => x.ScrimMatchId,
                        principalTable: "ScrimMatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrimMatch_RulesetId",
                table: "ScrimMatch",
                column: "RulesetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructedTeamPlayerMembership");

            migrationBuilder.DropTable(
                name: "RulesetActionRule");

            migrationBuilder.DropTable(
                name: "RulesetFacilityRule");

            migrationBuilder.DropTable(
                name: "RulesetItemCategoryRule");

            migrationBuilder.DropTable(
                name: "RulesetItemRule");

            migrationBuilder.DropTable(
                name: "RulesetOverlayConfiguration");

            migrationBuilder.DropTable(
                name: "ScrimAction");

            migrationBuilder.DropTable(
                name: "ScrimDamageAssist");

            migrationBuilder.DropTable(
                name: "ScrimDeath");

            migrationBuilder.DropTable(
                name: "ScrimFacilityControl");

            migrationBuilder.DropTable(
                name: "ScrimGrenadeAssist");

            migrationBuilder.DropTable(
                name: "ScrimMatchParticipatingPlayer");

            migrationBuilder.DropTable(
                name: "ScrimMatchRoundConfiguration");

            migrationBuilder.DropTable(
                name: "ScrimMatchTeamPointAdjustment");

            migrationBuilder.DropTable(
                name: "ScrimRevive");

            migrationBuilder.DropTable(
                name: "ScrimSpotAssist");

            migrationBuilder.DropTable(
                name: "ScrimVehicleDestruction");

            migrationBuilder.DropTable(
                name: "ConstructedTeam");

            migrationBuilder.DropTable(
                name: "ScrimMatch");

            migrationBuilder.DropTable(
                name: "ScrimMatchTeamResult");

            migrationBuilder.DropTable(
                name: "Ruleset");
        }
    }
}
