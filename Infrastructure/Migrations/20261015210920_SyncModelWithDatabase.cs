using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelWithDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpReputations_TenantId_IpAddress",
                table: "IpReputations");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "IpReputations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_IpReputations_TenantId_IpAddress",
                table: "IpReputations",
                columns: new[] { "TenantId", "IpAddress" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpReputations_TenantId_IpAddress",
                table: "IpReputations");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "IpReputations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpReputations_TenantId_IpAddress",
                table: "IpReputations",
                columns: new[] { "TenantId", "IpAddress" },
                unique: true);
        }
    }
}
