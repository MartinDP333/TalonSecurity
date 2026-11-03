using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProtectedSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OriginUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtectedSites_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedSites_Hostname",
                table: "ProtectedSites",
                column: "Hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedSites_TenantId",
                table: "ProtectedSites",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProtectedSites");
        }
    }
}
