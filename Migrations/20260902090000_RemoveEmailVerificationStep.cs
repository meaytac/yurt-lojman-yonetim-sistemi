using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using yurt_lojman_yonetim_sistemi.Data;

#nullable disable

namespace yurt_lojman_yonetim_sistemi.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260902090000_RemoveEmailVerificationStep")]
    public partial class RemoveEmailVerificationStep : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Applications
                SET Status = 'Pending',
                    UpdatedAt = COALESCE(UpdatedAt, datetime('now'))
                WHERE Status = 'EmailVerificationPending';
                """);

            migrationBuilder.Sql("""
                UPDATE ApplicationStatusHistories
                SET Status = 'Pending',
                    Note = 'Yetkili onayı bekleniyor.'
                WHERE Status = 'EmailVerificationPending';
                """);

            migrationBuilder.Sql("""
                DELETE FROM ApplicationAccessTokens
                WHERE Purpose = 'EmailVerification';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
