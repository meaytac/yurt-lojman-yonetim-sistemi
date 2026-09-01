using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTesislerAndUI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Floors",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Floors",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Buildings",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Buildings",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Dormitories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "HousingUnits",
                keyColumn: "Id",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Dormitories",
                columns: new[] { "Id", "CampusLocation", "IsActive", "Name", "TotalCapacity", "Type" },
                values: new object[] { 1, "Battalgazi Yerleskesi", true, "MTU Merkez Ogrenci Yurdu", 120, "Yurt" });

            migrationBuilder.InsertData(
                table: "HousingUnits",
                columns: new[] { "Id", "CampusLocation", "IsActive", "Name", "TotalCapacity", "Type" },
                values: new object[] { 1, "Battalgazi Yerleskesi", true, "MTU Personel Lojmanlari", 40, "Lojman" });

            migrationBuilder.InsertData(
                table: "Buildings",
                columns: new[] { "Id", "BlockName", "DormitoryId", "HousingUnitId" },
                values: new object[,]
                {
                    { 1, "A Blok", 1, null },
                    { 2, "L Blok", null, 1 }
                });

            migrationBuilder.InsertData(
                table: "Floors",
                columns: new[] { "Id", "BuildingId", "FloorNumber" },
                values: new object[,]
                {
                    { 1, 1, 1 },
                    { 2, 2, 1 }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "BlockFloorId", "Capacity", "CurrentOccupancy", "Price", "RoomNumber", "Status" },
                values: new object[,]
                {
                    { 1, 1, 4, 0, 2500m, "101", "Empty" },
                    { 2, 1, 4, 0, 2500m, "102", "Empty" },
                    { 3, 2, 1, 0, 5500m, "L101", "Empty" }
                });
        }
    }
}
