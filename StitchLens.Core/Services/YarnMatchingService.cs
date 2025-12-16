using Microsoft.EntityFrameworkCore;
using StitchLens.Core.ColorScience;
using StitchLens.Data;
using StitchLens.Data.Models;

namespace StitchLens.Core.Services;

public class YarnMatchingService : IYarnMatchingService {
    private readonly StitchLensDbContext _context;

    public YarnMatchingService(StitchLensDbContext context) {
        _context = context;
    }

    public async Task<List<YarnMatch>> MatchColorsToYarnAsync(
    List<ColorInfo> palette,
    int yarnBrandId,
    int totalStitchesInPattern,
    CraftType craftType) // ADD THIS PARAMETER
{
        // Load yarn colors for the brand AND matching craft type
        var yarnBrand = await _context.YarnBrands
            .FirstOrDefaultAsync(b => b.Id == yarnBrandId);

        if (yarnBrand == null)
            throw new InvalidOperationException($"Yarn brand {yarnBrandId} not found");

        // Verify brand matches craft type
        if (yarnBrand.CraftType != craftType) {
            throw new InvalidOperationException(
                $"Yarn brand '{yarnBrand.Name}' is for {yarnBrand.CraftType}, but pattern is for {craftType}");
        }

        var yarnColors = await _context.YarnColors
            .Where(y => y.YarnBrandId == yarnBrandId)
            .ToListAsync();

        var matches = new List<YarnMatch>();

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

            // Calculate yarn needed using BRAND-SPECIFIC yards per stitch
            // Use fractional yards and skeins instead of forcing integer values
            double yardsNeeded = paletteColor.PixelCount * (double)yarnBrand.YardsPerStitch;

            double estimatedSkeins =0.0;
            if (bestMatch.Yarn.YardsPerSkein >0)
                estimatedSkeins = yardsNeeded / bestMatch.Yarn.YardsPerSkein;

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
                StitchCount = paletteColor.PixelCount,
                DeltaE = bestMatch.DeltaE,
                YardsNeeded = Math.Round(yardsNeeded,2),
                EstimatedSkeins = Math.Round(estimatedSkeins,2)
            });
        }

        return matches.OrderByDescending(m => m.StitchCount).ToList();
    }
}