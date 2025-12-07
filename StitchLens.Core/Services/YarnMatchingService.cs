using Microsoft.EntityFrameworkCore;
using StitchLens.Core.ColorScience;
using StitchLens.Data;

namespace StitchLens.Core.Services;

public class YarnMatchingService : IYarnMatchingService {
    private readonly StitchLensDbContext _context;

    public YarnMatchingService(StitchLensDbContext context) {
        _context = context;
    }

    public async Task<List<YarnMatch>> MatchColorsToYarnAsync(
        List<ColorInfo> palette,
        int yarnBrandId,
        int totalStitchesInPattern)  // Add this parameter
    {
        // Load all yarn colors for the brand
        var yarnColors = await _context.YarnColors
            .Where(y => y.YarnBrandId == yarnBrandId)
            .ToListAsync();

        var matches = new List<YarnMatch>();
        var totalPixels = palette.Sum(p => p.PixelCount);

        foreach (var paletteColor in palette) {
            // Find best matching yarn using ΔE2000
            var bestMatch = yarnColors
                .Select(yarn => new {
                    Yarn = yarn,
                    DeltaE = ColorConverter.CalculateDeltaE2000(
                        paletteColor.Lab_L, paletteColor.Lab_A, paletteColor.Lab_B,
                        yarn.Lab_L, yarn.Lab_A, yarn.Lab_B)
                })
                .OrderBy(m => m.DeltaE)
                .First();

            // Calculate actual stitch count for this color
            // Proportion of pixels = proportion of stitches
            double colorProportion = (double)paletteColor.PixelCount / totalPixels;
            int actualStitchCount = (int)Math.Round(totalStitchesInPattern * colorProportion);

            // Calculate yarn needed
            // For tent stitch: 1 yard of yarn covers approximately 100 stitches
            double stitchesPerYard = 100.0;
            double baseYardsNeeded = actualStitchCount / stitchesPerYard;

            // Add 15% buffer for waste, mistakes, and coverage variations
            int yardsNeeded = (int)Math.Ceiling(baseYardsNeeded * 1.15);

            // Minimum 1 yard per color (can't buy less than that)
            if (yardsNeeded < 1) yardsNeeded = 1;

            int skeinsNeeded = (int)Math.Ceiling((double)yardsNeeded / bestMatch.Yarn.YardsPerSkein);

            matches.Add(new YarnMatch {
                YarnColorId = bestMatch.Yarn.Id,
                Code = bestMatch.Yarn.Code,
                Name = bestMatch.Yarn.Name,
                HexColor = bestMatch.Yarn.HexColor,
                R = paletteColor.R,
                G = paletteColor.G,
                B = paletteColor.B,
                Lab_L = bestMatch.Yarn.Lab_L,
                Lab_A = bestMatch.Yarn.Lab_A,
                Lab_B = bestMatch.Yarn.Lab_B,
                StitchCount = actualStitchCount,
                DeltaE = bestMatch.DeltaE,
                YardsNeeded = yardsNeeded,
                EstimatedSkeins = skeinsNeeded
            });
        }

        return matches.OrderByDescending(m => m.StitchCount).ToList();
    }
}