using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HighlightFilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoachingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HtmlContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachingReports_VideoJobs_VideoJobId",
                        column: x => x.VideoJobId,
                        principalTable: "VideoJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RallySegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartSeconds = table.Column<double>(type: "float", nullable: false),
                    EndSeconds = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RallySegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RallySegments_VideoJobs_VideoJobId",
                        column: x => x.VideoJobId,
                        principalTable: "VideoJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachingReports_VideoJobId",
                table: "CoachingReports",
                column: "VideoJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RallySegments_VideoJobId",
                table: "RallySegments",
                column: "VideoJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachingReports");

            migrationBuilder.DropTable(
                name: "RallySegments");

            migrationBuilder.DropTable(
                name: "VideoJobs");
        }
    }
}
