using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCountryLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_TenantId_CountryCode_OccurredAt",
                table: "SecurityAuditEvents",
                columns: new[] { "TenantId", "CountryCode", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditEvents_TenantId_CountryCode_OccurredAt",
                table: "SecurityAuditEvents");
        }
    }
}
