using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api_Celero.Migrations.ActivityLog
{
    /// <inheritdoc />
    public partial class InitialActivityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ScreenResolution = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BrowserLanguage = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AdditionalData = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PaymentStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Method = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    QueryString = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "INTEGER", nullable: true),
                    RequestTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ClientId",
                table: "ActivityLogs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CreatedAtUtc",
                table: "ActivityLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EventType",
                table: "ActivityLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EventType_CreatedAtUtc",
                table: "ActivityLogs",
                columns: new[] { "EventType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Fingerprint",
                table: "ActivityLogs",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_IpAddress",
                table: "ActivityLogs",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_IpAddress",
                table: "RequestLogs",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_IpAddress_RequestTimeUtc",
                table: "RequestLogs",
                columns: new[] { "IpAddress", "RequestTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_Method",
                table: "RequestLogs",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_RequestTimeUtc",
                table: "RequestLogs",
                column: "RequestTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_StatusCode",
                table: "RequestLogs",
                column: "StatusCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "RequestLogs");
        }
    }
}
