using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFacilityAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserFacilityAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DormitoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    HousingUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFacilityAssignments", x => x.Id);
                    table.CheckConstraint("CK_UserFacilityAssignment_OneFacility", "([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserFacilityAssignments_AspNetUsers_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFacilityAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFacilityAssignments_Dormitories_DormitoryId",
                        column: x => x.DormitoryId,
                        principalTable: "Dormitories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFacilityAssignments_HousingUnits_HousingUnitId",
                        column: x => x.HousingUnitId,
                        principalTable: "HousingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilityAssignments_AssignedById",
                table: "UserFacilityAssignments",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilityAssignments_DormitoryId",
                table: "UserFacilityAssignments",
                column: "DormitoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilityAssignments_HousingUnitId",
                table: "UserFacilityAssignments",
                column: "HousingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilityAssignments_UserId",
                table: "UserFacilityAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFacilityAssignments");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");
        }
    }
}
