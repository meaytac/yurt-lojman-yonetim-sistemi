using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepairPeriodDays",
                table: "Requests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetRepairDate",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CleaningTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 800, nullable: true),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleaningTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodicMaintenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    NextMaintenanceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastMaintenanceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicMaintenances", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CleaningTasks");

            migrationBuilder.DropTable(
                name: "PeriodicMaintenances");

            migrationBuilder.DropColumn(
                name: "RepairPeriodDays",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "TargetRepairDate",
                table: "Requests");
        }
    }
}
