using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using StitchLens.Core.ColorScience;
namespace StitchLens.Core.Services;
public interface IGridGenerationService {
    Task<StitchGrid> GenerateStitchGridAsync(
        byte[] quantizedImageData,
        int targetWidth,
        int targetHeight,
        List<YarnMatch> yarnMatches);
}
public class GridGenerationService : IGridGenerationService {
    private readonly string[] _symbols = new[]
    {
        "•", "○", "◆", "◇", "■", "□", "▲", "△", "★", "☆",
        "●", "◉", "▪", "▫", "◘", "◙", "▼", "▽", "◊", "◈"
    };
    public async Task<StitchGrid> GenerateStitchGridAsync(
        byte[] quantizedImageData,
        int targetWidth,
        int targetHeight,
        List<YarnMatch> yarnMatches) {
        return await Task.Run(() => {
            // CHANGED: Load as Rgba32 to detect transparency
            using var image = Image.Load<Rgba32>(quantizedImageData);
            // Resize image to exact stitch dimensions
            image.Mutate(x => x.Resize(targetWidth, targetHeight));
            var grid = new StitchGrid {
                Width = targetWidth,
                Height = targetHeight,
                Cells = new StitchCell[targetWidth, targetHeight]
            };
            // Build color-to-index lookup
            var colorLookup = new Dictionary<string, int>();
            for (int i = 0; i < yarnMatches.Count; i++) {
                var yarn = yarnMatches[i];
                var key = $"{yarn.R},{yarn.G},{yarn.B}";
                colorLookup[key] = i;
            }
            // Process each pixel/stitch
            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++) {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++) {
                        var pixel = row[x];

                        // CHANGED: Handle transparent pixels
                        if (pixel.A < 128) {
                            grid.Cells[x, y] = new StitchCell {
                                YarnMatchIndex = -1,
                                Symbol = "",
                                HexColor = "#FFFFFF",
                                IsTransparent = true
                            };
                            continue;
                        }

                        var key = $"{pixel.R},{pixel.G},{pixel.B}";
                        // Find matching yarn
                        int yarnIndex = 0;
                        if (colorLookup.ContainsKey(key)) {
                            yarnIndex = colorLookup[key];
                        }
                        else {
                            // Find closest match by LAB distance
                            var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);
                            yarnIndex = FindClosestYarnMatch(l, a, b, yarnMatches);
                        }
                        var symbol = yarnIndex < _symbols.Length
                            ? _symbols[yarnIndex]
                            : (yarnIndex + 1).ToString();
                        grid.Cells[x, y] = new StitchCell {
                            YarnMatchIndex = yarnIndex,
                            Symbol = symbol,
                            HexColor = yarnMatches[yarnIndex].HexColor,
                            IsTransparent = false // CHANGED: Explicitly set to false for opaque cells
                        };
                    }
                }
            });
            return grid;
        });
    }
    private int FindClosestYarnMatch(double l, double a, double b, List<YarnMatch> yarnMatches) {
        int bestIndex = 0;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < yarnMatches.Count; i++) {
            var yarn = yarnMatches[i];
            var distance = ColorConverter.CalculateLabDistance(
                l, a, b,
                yarn.Lab_L, yarn.Lab_A, yarn.Lab_B);
            if (distance < bestDistance) {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }
}