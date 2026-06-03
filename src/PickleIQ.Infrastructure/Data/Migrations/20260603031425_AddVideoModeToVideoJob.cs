using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickleIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoModeToVideoJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "VideoJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Match");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "VideoJobs");
        }
    }
}
