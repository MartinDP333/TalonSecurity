using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class AddSecurityAuditEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecurityAuditEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                HttpMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SourceIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                StatusCode = table.Column<int>(type: "int", nullable: true),
                Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SecurityAuditEvents", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SecurityAuditEvents_TenantId_OccurredAt",
            table: "SecurityAuditEvents",
            columns: new[] { "TenantId", "OccurredAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SecurityAuditEvents");
    }
}
