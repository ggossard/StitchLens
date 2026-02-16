using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StitchLens.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualBillingAndPerPatternPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StripePriceId",
                table: "TierConfigurations",
                newName: "StripePerPatternPriceId");

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualPrice",
                table: "TierConfigurations",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerPatternPrice",
                table: "TierConfigurations",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeAnnualPriceId",
                table: "TierConfigurations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeMonthlyPriceId",
                table: "TierConfigurations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Subscriptions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.Sql("UPDATE TierConfigurations SET StripeMonthlyPriceId = StripePerPatternPriceId WHERE Tier IN (1, 2);");
            migrationBuilder.Sql("UPDATE TierConfigurations SET StripePerPatternPriceId = NULL WHERE Tier IN (1, 2);");
            migrationBuilder.Sql("UPDATE TierConfigurations SET PerPatternPrice = 5.95 WHERE Tier = 0;");
            migrationBuilder.Sql("UPDATE TierConfigurations SET AnnualPrice = MonthlyPrice * 10 WHERE Tier IN (1, 2);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualPrice",
                table: "TierConfigurations");

            migrationBuilder.DropColumn(
                name: "PerPatternPrice",
                table: "TierConfigurations");

            migrationBuilder.DropColumn(
                name: "StripeAnnualPriceId",
                table: "TierConfigurations");

            migrationBuilder.DropColumn(
                name: "StripeMonthlyPriceId",
                table: "TierConfigurations");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Subscriptions");

            migrationBuilder.Sql("UPDATE TierConfigurations SET StripePerPatternPriceId = StripeMonthlyPriceId WHERE Tier IN (1, 2);");

            migrationBuilder.RenameColumn(
                name: "StripePerPatternPriceId",
                table: "TierConfigurations",
                newName: "StripePriceId");
        }
    }
}
