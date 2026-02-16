using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StitchLens.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuotasToPatternCreationAndTrackProjectDownloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DownloadQuota",
                table: "TierConfigurations",
                newName: "PatternCreationQuota");

            migrationBuilder.RenameColumn(
                name: "DownloadQuota",
                table: "Subscriptions",
                newName: "PatternCreationQuota");

            migrationBuilder.RenameColumn(
                name: "LastDownloadDate",
                table: "AspNetUsers",
                newName: "LastPatternCreationDate");

            migrationBuilder.RenameColumn(
                name: "DownloadsThisMonth",
                table: "AspNetUsers",
                newName: "PatternsCreatedThisMonth");

            migrationBuilder.AddColumn<int>(
                name: "Downloads",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Downloads",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "PatternCreationQuota",
                table: "TierConfigurations",
                newName: "DownloadQuota");

            migrationBuilder.RenameColumn(
                name: "PatternCreationQuota",
                table: "Subscriptions",
                newName: "DownloadQuota");

            migrationBuilder.RenameColumn(
                name: "PatternsCreatedThisMonth",
                table: "AspNetUsers",
                newName: "DownloadsThisMonth");

            migrationBuilder.RenameColumn(
                name: "LastPatternCreationDate",
                table: "AspNetUsers",
                newName: "LastDownloadDate");
        }
    }
}
