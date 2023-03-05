﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using squittal.ScrimPlanetmans.App.Data;

namespace squittal.ScrimPlanetmans.App.Migrations
{
    [DbContext(typeof(PlanetmansDbContext))]
    [Migration("20200420193620_AddVehicleVehicleFactionModels")]
    partial class AddVehicleVehicleFactionModels
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.FacilityType", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("FacilityType");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Faction", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("CodeTag")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ImageId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("UserSelectable")
                        .HasColumnType("bit");

                    b.HasKey("Id");

                    b.ToTable("Faction");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Item", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("FactionId")
                        .HasColumnType("int");

                    b.Property<int?>("ImageId")
                        .HasColumnType("int");

                    b.Property<bool>("IsVehicleWeapon")
                        .HasColumnType("bit");

                    b.Property<int?>("ItemCategoryId")
                        .HasColumnType("int");

                    b.Property<int?>("ItemTypeId")
                        .HasColumnType("int");

                    b.Property<int?>("MaxStackSize")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Item");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.ItemCategory", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<int>("Domain")
                        .HasColumnType("int");

                    b.Property<bool>("IsWeaponCategory")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("ItemCategory");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Loadout", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("CodeName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("FactionId")
                        .HasColumnType("int");

                    b.Property<int>("ProfileId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Loadout");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.MapRegion", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<int>("FacilityId")
                        .HasColumnType("int");

                    b.Property<string>("FacilityName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FacilityType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("FacilityTypeId")
                        .HasColumnType("int");

                    b.Property<int>("ZoneId")
                        .HasColumnType("int");

                    b.HasKey("Id", "FacilityId");

                    b.ToTable("MapRegion");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Profile", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<int>("FactionId")
                        .HasColumnType("int");

                    b.Property<int>("ImageId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("ProfileTypeId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Profile");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Vehicle", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<int?>("Cost")
                        .HasColumnType("int");

                    b.Property<int?>("CostResourceId")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ImageId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TypeId")
                        .HasColumnType("int");

                    b.Property<string>("TypeName")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Vehicle");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.VehicleFaction", b =>
                {
                    b.Property<int>("VehicleId")
                        .HasColumnType("int");

                    b.Property<int>("FactionId")
                        .HasColumnType("int");

                    b.HasKey("VehicleId", "FactionId");

                    b.ToTable("VehicleFaction");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.World", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("World");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.Zone", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Code")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("HexSize")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Zone");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.Ruleset", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DateLastModified")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsCustomDefault")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsDefault")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Ruleset");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.RulesetActionRule", b =>
                {
                    b.Property<int>("RulesetId")
                        .HasColumnType("int");

                    b.Property<int>("ScrimActionType")
                        .HasColumnType("int");

                    b.Property<bool>("DeferToItemCategoryRules")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<int>("Points")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.HasKey("RulesetId", "ScrimActionType");

                    b.ToTable("RulesetActionRule");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.RulesetItemCategoryRule", b =>
                {
                    b.Property<int>("RulesetId")
                        .HasColumnType("int");

                    b.Property<int>("ItemCategoryId")
                        .HasColumnType("int");

                    b.Property<int>("Points")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.HasKey("RulesetId", "ItemCategoryId");

                    b.ToTable("RulesetItemCategoryRule");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.ScrimAction", b =>
                {
                    b.Property<int>("Action")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Action");

                    b.ToTable("ScrimAction");
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.Models.Planetside.VehicleFaction", b =>
                {
                    b.HasOne("squittal.ScrimPlanetmans.Models.Planetside.Vehicle", "Vehicle")
                        .WithMany("Faction")
                        .HasForeignKey("VehicleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.RulesetActionRule", b =>
                {
                    b.HasOne("squittal.ScrimPlanetmans.ScrimMatch.Models.Ruleset", "Ruleset")
                        .WithMany("ActionRules")
                        .HasForeignKey("RulesetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("squittal.ScrimPlanetmans.ScrimMatch.Models.RulesetItemCategoryRule", b =>
                {
                    b.HasOne("squittal.ScrimPlanetmans.ScrimMatch.Models.Ruleset", "Ruleset")
                        .WithMany("ItemCategoryRules")
                        .HasForeignKey("RulesetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
