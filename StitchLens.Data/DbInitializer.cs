using System.Text.Json;
using StitchLens.Data.Models;

namespace StitchLens.Data;

public static class DbInitializer {
    public static void Initialize(StitchLensDbContext context, string contentRootPath) {
        context.Database.EnsureCreated();

        // Check if brands already exist
        if (context.YarnBrands.Any())
            return;

        // Define yarn brands to load
        var brandsToLoad = new[]
        {
            new YarnBrandConfig
            {
                Name = "DMC",
                Country = "France",
                FileName = "dmc-colors.json",
                YardsPerSkein = 8,
                IsActive = true
            },
            new YarnBrandConfig
            {
                Name = "Appleton",
                Country = "UK",
                FileName = "appleton-colors.json",
                YardsPerSkein = 8, // Update with actual value if different
                IsActive = true
            },
            new YarnBrandConfig
            {
                Name = "Paternayan",
                Country = "USA",
                FileName = "paternayan-colors.json",
                YardsPerSkein = 8, // 8.8 yards per skein (10 strands x 32" each)
                IsActive = true
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
            IsActive = config.IsActive,
            YardsPerSkein = config.YardsPerSkein
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
                    YardsPerSkein = brand.YardsPerSkein
                });
            }

            context.SaveChanges();
            Console.WriteLine($"✓ Seeded {colorData.Count} {config.Name} colors");
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