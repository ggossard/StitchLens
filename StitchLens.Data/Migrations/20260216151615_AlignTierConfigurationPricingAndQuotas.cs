using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StitchLens.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignTierConfigurationPricingAndQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 3, MonthlyPrice = 12.95 WHERE Tier = 1;");
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 30, MonthlyPrice = 35.95 WHERE Tier = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 10, MonthlyPrice = 12.99 WHERE Tier = 1;");
            migrationBuilder.Sql("UPDATE TierConfigurations SET PatternCreationQuota = 100, MonthlyPrice = 49.99 WHERE Tier = 2;");
        }
    }
}
