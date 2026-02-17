using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StitchLens.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakePayAsYouGoPatternCreationUnlimited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 2147483647, PatternCreationDailyLimit = 2147483647 WHERE Tier = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 1, PatternCreationDailyLimit = 3 WHERE Tier = 0;");
        }
    }
}
