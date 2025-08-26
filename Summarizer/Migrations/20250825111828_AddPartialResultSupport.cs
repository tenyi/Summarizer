using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Summarizer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialResultSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartialResults",
                columns: table => new
                {
                    PartialResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CompletedSegments = table.Column<string>(type: "TEXT", nullable: false),
                    TotalSegments = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionPercentage = table.Column<double>(type: "REAL", nullable: false),
                    PartialSummary = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: false),
                    CancellationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserAccepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcceptedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTextSample = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    ProcessingTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    UserComment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartialResults", x => x.PartialResultId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartialResults_BatchId",
                table: "PartialResults",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PartialResults_CancellationTime",
                table: "PartialResults",
                column: "CancellationTime");

            migrationBuilder.CreateIndex(
                name: "IX_PartialResults_Status",
                table: "PartialResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PartialResults_UserId",
                table: "PartialResults",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartialResults");
        }
    }
}
