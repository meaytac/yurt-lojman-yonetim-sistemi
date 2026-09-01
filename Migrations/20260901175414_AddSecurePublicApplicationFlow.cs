using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurePublicApplicationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_AspNetUsers_UserId",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "HousingUnits",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationConditions",
                table: "HousingUnits",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "HousingUnits",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApplicationOpen",
                table: "HousingUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "HousingUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicDescription",
                table: "HousingUnits",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Dormitories",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationConditions",
                table: "Dormitories",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Dormitories",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApplicationOpen",
                table: "Dormitories",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Dormitories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicDescription",
                table: "Dormitories",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Applications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationSentAt",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantEmail",
                table: "Applications",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantFullName",
                table: "Applications",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantNote",
                table: "Applications",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantPhoneNumber",
                table: "Applications",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRole",
                table: "Applications",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantStudentStaffNo",
                table: "Applications",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantTcNo",
                table: "Applications",
                type: "TEXT",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedRoomId",
                table: "Applications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedById",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "Applications",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "Applications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "Applications",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCode",
                table: "Applications",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RequestedDormitoryId",
                table: "Applications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedHousingUnitId",
                table: "Applications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Applications",
                type: "TEXT",
                nullable: false,
                defaultValue: "RegisteredUser");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Applications",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationAccessTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestIpHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationAccessTokens_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ChangedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_AspNetUsers_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ApprovedRoomId",
                table: "Applications",
                column: "ApprovedRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_DecidedById",
                table: "Applications",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications",
                column: "IdempotencyKeyHash");

            migrationBuilder.Sql("UPDATE Applications SET ReferenceCode = 'RG' || Id WHERE ReferenceCode = ''");
            migrationBuilder.Sql("UPDATE Applications SET Source = 'RegisteredUser' WHERE Source = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ReferenceCode",
                table: "Applications",
                column: "ReferenceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_RequestedDormitoryId",
                table: "Applications",
                column: "RequestedDormitoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_RequestedHousingUnitId",
                table: "Applications",
                column: "RequestedHousingUnitId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Applications_RequestedFacility",
                table: "Applications",
                sql: "([RequestedDormitoryId] IS NULL AND [RequestedHousingUnitId] IS NULL) OR ([RequestedDormitoryId] IS NOT NULL AND [RequestedHousingUnitId] IS NULL) OR ([RequestedDormitoryId] IS NULL AND [RequestedHousingUnitId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAccessTokens_ApplicationId_Purpose_UsedAt_ExpiresAt",
                table: "ApplicationAccessTokens",
                columns: new[] { "ApplicationId", "Purpose", "UsedAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAccessTokens_TokenHash",
                table: "ApplicationAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId",
                table: "ApplicationStatusHistories",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ChangedById",
                table: "ApplicationStatusHistories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_SentAt_CreatedAt",
                table: "EmailOutboxMessages",
                columns: new[] { "SentAt", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_AspNetUsers_DecidedById",
                table: "Applications",
                column: "DecidedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_AspNetUsers_UserId",
                table: "Applications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Dormitories_RequestedDormitoryId",
                table: "Applications",
                column: "RequestedDormitoryId",
                principalTable: "Dormitories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_HousingUnits_RequestedHousingUnitId",
                table: "Applications",
                column: "RequestedHousingUnitId",
                principalTable: "HousingUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Rooms_ApprovedRoomId",
                table: "Applications",
                column: "ApprovedRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_AspNetUsers_DecidedById",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_AspNetUsers_UserId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Dormitories_RequestedDormitoryId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_HousingUnits_RequestedHousingUnitId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Rooms_ApprovedRoomId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "ApplicationAccessTokens");

            migrationBuilder.DropTable(
                name: "ApplicationStatusHistories");

            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Applications_ApprovedRoomId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_DecidedById",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_IdempotencyKeyHash",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_ReferenceCode",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_RequestedDormitoryId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_RequestedHousingUnitId",
                table: "Applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Applications_RequestedFacility",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "ApplicationConditions",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "IsApplicationOpen",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "PublicDescription",
                table: "HousingUnits");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "ApplicationConditions",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "IsApplicationOpen",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "PublicDescription",
                table: "Dormitories");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ActivationSentAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantEmail",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantFullName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantNote",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantPhoneNumber",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantRole",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantStudentStaffNo",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApplicantTcNo",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ApprovedRoomId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "DecidedById",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ReferenceCode",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RequestedDormitoryId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RequestedHousingUnitId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Applications");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Applications",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_AspNetUsers_UserId",
                table: "Applications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
