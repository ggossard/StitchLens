using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StitchLens.Data.Models;

namespace StitchLens.Data;

public static class DbInitializer {
    public static void Initialize(StitchLensDbContext context, string contentRootPath) {
        context.Database.Migrate();

        // Seed tier configurations FIRST
        if (!context.TierConfigurations.Any()) {
            var tiers = new[]
            {
                new TierConfiguration
                {
                    Tier = SubscriptionTier.PayAsYouGo,
                    Name = "Pay As You Go",
                    Description = "Try it out",
                    PatternCreationQuota = 1,
                    PatternCreationDailyLimit = 3,
                    AllowCommercialUse = false,
                    PrioritySupport = false,
                    MonthlyPrice = 0,
                    AnnualPrice = null,
                    PerPatternPrice = 5.95m,
                    StripeMonthlyPriceId = null,
                    StripeAnnualPriceId = null,
                    StripePerPatternPriceId = null
                },
                new TierConfiguration
                {
                    Tier = SubscriptionTier.Hobbyist,
                    Name = "Hobbyist",
                    Description = "For regular stitchers",
                    PatternCreationQuota = 3,
                    PatternCreationDailyLimit = 20,
                    AllowCommercialUse = false,
                    PrioritySupport = false,
                    MonthlyPrice = 12.95m,
                    AnnualPrice = 129.50m,
                    PerPatternPrice = null,
                    StripeMonthlyPriceId = "price_1SbpSSFiTiD9qoU887veIxiO",
                    StripeAnnualPriceId = null,
                    StripePerPatternPriceId = null
                },
                new TierConfiguration
                {
                    Tier = SubscriptionTier.Creator,
                    Name = "Creator",
                    Description = "Sell your finished pieces",
                    PatternCreationQuota = 30,
                    PatternCreationDailyLimit = 100,
                    AllowCommercialUse = true,
                    PrioritySupport = true,
                    MonthlyPrice = 35.95m,
                    AnnualPrice = 359.50m,
                    PerPatternPrice = null,
                    StripeMonthlyPriceId = "price_1SbpSaFiTiD9qoU8yrkT8tOE",
                    StripeAnnualPriceId = null,
                    StripePerPatternPriceId = null
                },
                new TierConfiguration
                {
                    Tier = SubscriptionTier.Custom,
                    Name = "Custom",
                    Description = "Enterprise solutions",
                    PatternCreationQuota = int.MaxValue,
                    PatternCreationDailyLimit = int.MaxValue,
                    AllowCommercialUse = true,
                    PrioritySupport = true,
                    MonthlyPrice = 0,
                    AnnualPrice = null,
                    PerPatternPrice = null,
                    StripeMonthlyPriceId = null,
                    StripeAnnualPriceId = null,
                    StripePerPatternPriceId = null
                }
            };

            context.TierConfigurations.AddRange(tiers);
            context.SaveChanges();
            Console.WriteLine("Seeded tier configurations");
        }

        // Check if brands already exist
        if (context.YarnBrands.Any())
            return;

        // Define yarn brands to load - CRAFT SPECIFIC
        var brandsToLoad = new[]
        {
            // NEEDLEPOINT BRANDS
            new YarnBrandConfig
            {
                Name = "DMC Tapestry Wool",
                Country = "France",
                FileName = "dmc-colors.json",
                YardsPerSkein = 9,  // 8.8 rounded up
                YardsPerStitch = 0.007m,  // CORRECTED
                CraftType = CraftType.Needlepoint,
                IsActive = true
            },
            new YarnBrandConfig
            {
                Name = "Appleton Crewel Wool",
                Country = "UK",
                FileName = "appleton-colors.json",
                YardsPerSkein = 8,
                YardsPerStitch = 0.007m,  // CORRECTED
                CraftType = CraftType.Needlepoint,
                IsActive = true
            },
            new YarnBrandConfig
            {
                Name = "Paternayan Persian Wool",
                Country = "USA",
                FileName = "paternayan-colors.json",
                YardsPerSkein = 8,
                YardsPerStitch = 0.007m,  // CORRECTED
                CraftType = CraftType.Needlepoint,
                IsActive = true
            },
    
            // CROSS-STITCH BRANDS
            new YarnBrandConfig
            {
                Name = "DMC Embroidery Floss",
                Country = "France",
                FileName = "dmc-colors.json",
                YardsPerSkein = 9,  // 8.7 rounded up
                YardsPerStitch = 0.006m,  // CORRECTED (was 0.083!)
                CraftType = CraftType.CrossStitch,
                IsActive = true
            },
            new YarnBrandConfig
            {
                Name = "Anchor Embroidery Floss",
                Country = "UK",
                FileName = "anchor-colors.json",
                YardsPerSkein = 9,
                YardsPerStitch = 0.006m,  // CORRECTED
                CraftType = CraftType.CrossStitch,
                IsActive = false
            }
        };

        // Load each brand and its colors
        foreach (var brandConfig in brandsToLoad) {
            LoadYarnBrand(context, contentRootPath, brandConfig);
        }
    }

    private static void LoadYarnBrand(
        StitchLensDbContext context,
        string contentRootPath,
        YarnBrandConfig config) {
        // Add the brand
        var brand = new YarnBrand {
            Name = config.Name,
            Country = config.Country,
            CraftType = config.CraftType,
            YardsPerStitch = config.YardsPerStitch,
            IsActive = config.IsActive
        };

        context.YarnBrands.Add(brand);
        context.SaveChanges(); // Save to get the brand ID

        // Load colors from JSON
        var jsonPath = Path.Combine(contentRootPath, "SeedData", config.FileName);

        if (!File.Exists(jsonPath)) {
            Console.WriteLine($"Warning: {config.Name} colors file not found at {jsonPath}");
            return;
        }

        try {
            var jsonContent = File.ReadAllText(jsonPath);
            var colorData = JsonSerializer.Deserialize<List<YarnColorJson>>(jsonContent);

            if (colorData == null || !colorData.Any()) {
                Console.WriteLine($"Warning: No colors found in {config.FileName}");
                return;
            }

            foreach (var color in colorData) {
                context.YarnColors.Add(new YarnColor {
                    YarnBrandId = brand.Id,
                    Code = color.code,
                    Name = color.name,
                    HexColor = color.hex,
                    Lab_L = color.lab_l,
                    Lab_A = color.lab_a,
                    Lab_B = color.lab_b,
                    YardsPerSkein = config.YardsPerSkein
                });
            }

            context.SaveChanges();
            Console.WriteLine($"✓ Seeded {colorData.Count} colors for {config.Name} ({config.CraftType})");
        }
        catch (Exception ex) {
            Console.WriteLine($"Error loading {config.Name} colors: {ex.Message}");
        }
    }

    // Configuration class for yarn brands
    private class YarnBrandConfig {
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        public string FileName { get; set; } = "";
        public int YardsPerSkein { get; set; }
        public decimal YardsPerStitch { get; set; }
        public CraftType CraftType { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // JSON deserialization class (works for all brands with same JSON structure)
    private class YarnColorJson {
        public string code { get; set; } = "";
        public string name { get; set; } = "";
        public string hex { get; set; } = "";
        public double lab_l { get; set; }
        public double lab_a { get; set; }
        public double lab_b { get; set; }
    }
}
