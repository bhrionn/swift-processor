using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwiftMessageProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MessageType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    RawMessage = table.Column<string>(type: "NTEXT", nullable: false),
                    ParsedData = table.Column<string>(type: "NTEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "DATETIME2", nullable: false),
                    ErrorDetails = table.Column<string>(type: "NTEXT", nullable: true),
                    Metadata = table.Column<string>(type: "NTEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EventData = table.Column<string>(type: "NTEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MessageType",
                table: "Messages",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ProcessedAt",
                table: "Messages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Status",
                table: "Messages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Type_ProcessedAt",
                table: "Messages",
                columns: new[] { "MessageType", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAudit_EventType",
                table: "SystemAudit",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAudit_Timestamp",
                table: "SystemAudit",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "SystemAudit");
        }
    }
}
