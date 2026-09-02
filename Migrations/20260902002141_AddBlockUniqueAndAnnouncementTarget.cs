using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockUniqueAndAnnouncementTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Buildings_DormitoryId",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_HousingUnitId",
                table: "Buildings");

            migrationBuilder.AddColumn<int>(
                name: "TargetFacilityId",
                table: "Announcements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetFacilityName",
                table: "Announcements",
                type: "TEXT",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Announcements",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "TargetFacilityId", "TargetFacilityName" },
                values: new object[] { null, "Tüm Tesisler" });

            migrationBuilder.Sql(@"
                DELETE FROM Buildings
                WHERE DormitoryId IS NOT NULL
                  AND Id NOT IN (
                      SELECT MIN(Id)
                      FROM Buildings
                      WHERE DormitoryId IS NOT NULL
                      GROUP BY DormitoryId, BlockName
                  );
            ");

            migrationBuilder.Sql(@"
                DELETE FROM Buildings
                WHERE HousingUnitId IS NOT NULL
                  AND Id NOT IN (
                      SELECT MIN(Id)
                      FROM Buildings
                      WHERE HousingUnitId IS NOT NULL
                      GROUP BY HousingUnitId, BlockName
                  );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_DormitoryId_BlockName",
                table: "Buildings",
                columns: new[] { "DormitoryId", "BlockName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_HousingUnitId_BlockName",
                table: "Buildings",
                columns: new[] { "HousingUnitId", "BlockName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Buildings_DormitoryId_BlockName",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_HousingUnitId_BlockName",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "TargetFacilityId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "TargetFacilityName",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_DormitoryId",
                table: "Buildings",
                column: "DormitoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_HousingUnitId",
                table: "Buildings",
                column: "HousingUnitId");
        }
    }
}
