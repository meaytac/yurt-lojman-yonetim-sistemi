using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class RenameExternalApplicantAndIdempotencyPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Applications SET Source = 'ExternalApplicant' WHERE Source = 'PublicVisitor'");

            migrationBuilder.DropIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyPayloadHash",
                table: "Applications",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications",
                column: "IdempotencyKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Applications SET Source = 'PublicVisitor' WHERE Source = 'ExternalApplicant'");

            migrationBuilder.DropIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IdempotencyPayloadHash",
                table: "Applications");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications",
                column: "IdempotencyKeyHash");
        }
    }
}
