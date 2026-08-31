using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScopeToOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DormitoryId",
                table: "StaffAssignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HousingUnitId",
                table: "StaffAssignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DormitoryId",
                table: "FaultReports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HousingUnitId",
                table: "FaultReports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_DormitoryId",
                table: "StaffAssignments",
                column: "DormitoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAssignments_HousingUnitId",
                table: "StaffAssignments",
                column: "HousingUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffAssignments_OneFacility",
                table: "StaffAssignments",
                sql: "([DormitoryId] IS NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FaultReports_DormitoryId",
                table: "FaultReports",
                column: "DormitoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultReports_HousingUnitId",
                table: "FaultReports",
                column: "HousingUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FaultReports_OneFacility",
                table: "FaultReports",
                sql: "([DormitoryId] IS NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NOT NULL AND [HousingUnitId] IS NULL) OR ([DormitoryId] IS NULL AND [HousingUnitId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FaultReports_Dormitories_DormitoryId",
                table: "FaultReports",
                column: "DormitoryId",
                principalTable: "Dormitories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FaultReports_HousingUnits_HousingUnitId",
                table: "FaultReports",
                column: "HousingUnitId",
                principalTable: "HousingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAssignments_Dormitories_DormitoryId",
                table: "StaffAssignments",
                column: "DormitoryId",
                principalTable: "Dormitories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAssignments_HousingUnits_HousingUnitId",
                table: "StaffAssignments",
                column: "HousingUnitId",
                principalTable: "HousingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FaultReports_Dormitories_DormitoryId",
                table: "FaultReports");

            migrationBuilder.DropForeignKey(
                name: "FK_FaultReports_HousingUnits_HousingUnitId",
                table: "FaultReports");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffAssignments_Dormitories_DormitoryId",
                table: "StaffAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffAssignments_HousingUnits_HousingUnitId",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StaffAssignments_DormitoryId",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StaffAssignments_HousingUnitId",
                table: "StaffAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffAssignments_OneFacility",
                table: "StaffAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FaultReports_DormitoryId",
                table: "FaultReports");

            migrationBuilder.DropIndex(
                name: "IX_FaultReports_HousingUnitId",
                table: "FaultReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FaultReports_OneFacility",
                table: "FaultReports");

            migrationBuilder.DropColumn(
                name: "DormitoryId",
                table: "StaffAssignments");

            migrationBuilder.DropColumn(
                name: "HousingUnitId",
                table: "StaffAssignments");

            migrationBuilder.DropColumn(
                name: "DormitoryId",
                table: "FaultReports");

            migrationBuilder.DropColumn(
                name: "HousingUnitId",
                table: "FaultReports");
        }
    }
}
