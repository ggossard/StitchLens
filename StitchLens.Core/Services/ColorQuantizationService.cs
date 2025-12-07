using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;
using StitchLens.Core.ColorScience;
using Image = SixLabors.ImageSharp.Image;

namespace StitchLens.Core.Services;

public class ColorQuantizationService : IColorQuantizationService {
    public async Task<QuantizedResult> QuantizeAsync(byte[] imageData, int maxColors) {
        return await Task.Run(() => {
            using var image = Image.Load<Rgb24>(imageData);

            // Step 1: Extract all unique colors from image
            var pixels = ExtractPixels(image);

            // Step 2: Run K-means clustering in LAB space
            var clusters = KMeansClustering(pixels, maxColors);

            // Step 3: Create quantized image using cluster centers
            var quantizedImageData = ApplyQuantization(image, clusters);

            // Step 4: Build palette info
            var palette = clusters.Select(c => new ColorInfo {
                R = c.R,
                G = c.G,
                B = c.B,
                Lab_L = c.Lab_L,
                Lab_A = c.Lab_A,
                Lab_B = c.Lab_B,
                PixelCount = c.PixelCount
            }).ToList();

            return new QuantizedResult {
                QuantizedImageData = quantizedImageData,
                Palette = palette
            };
        });
    }

    private List<LabPixel> ExtractPixels(Image<Rgb24> image) {
        var pixels = new List<LabPixel>();

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++) {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++) {
                    var pixel = row[x];
                    var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);

                    pixels.Add(new LabPixel {
                        R = pixel.R,
                        G = pixel.G,
                        B = pixel.B,
                        Lab_L = l,
                        Lab_A = a,
                        Lab_B = b
                    });
                }
            }
        });

        return pixels;
    }

    private List<ColorCluster> KMeansClustering(List<LabPixel> pixels, int k, int maxIterations = 20) {
        var random = new Random(42); // Fixed seed for reproducibility

        // Initialize cluster centers randomly from existing pixels
        var clusters = pixels
            .OrderBy(_ => random.Next())
            .Take(k)
            .Select(p => new ColorCluster {
                R = p.R,
                G = p.G,
                B = p.B,
                Lab_L = p.Lab_L,
                Lab_A = p.Lab_A,
                Lab_B = p.Lab_B
            })
            .ToList();

        // K-means iterations
        for (int iteration = 0; iteration < maxIterations; iteration++) {
            // Reset cluster assignments
            foreach (var cluster in clusters) {
                cluster.AssignedPixels.Clear();
            }

            // Assign each pixel to nearest cluster (in LAB space)
            foreach (var pixel in pixels) {
                var nearestCluster = clusters
                    .OrderBy(c => ColorConverter.CalculateLabDistance(
                        pixel.Lab_L, pixel.Lab_A, pixel.Lab_B,
                        c.Lab_L, c.Lab_A, c.Lab_B))
                    .First();

                nearestCluster.AssignedPixels.Add(pixel);
            }

            // Recalculate cluster centers
            bool centersChanged = false;
            foreach (var cluster in clusters) {
                if (cluster.AssignedPixels.Count == 0) continue;

                var oldL = cluster.Lab_L;
                var oldA = cluster.Lab_A;
                var oldB = cluster.Lab_B;

                // Calculate mean LAB values
                cluster.Lab_L = cluster.AssignedPixels.Average(p => p.Lab_L);
                cluster.Lab_A = cluster.AssignedPixels.Average(p => p.Lab_A);
                cluster.Lab_B = cluster.AssignedPixels.Average(p => p.Lab_B);

                // Convert back to RGB for display
                var (r, g, b) = ColorConverter.LabToRgb(cluster.Lab_L, cluster.Lab_A, cluster.Lab_B);
                cluster.R = r;
                cluster.G = g;
                cluster.B = b;

                cluster.PixelCount = cluster.AssignedPixels.Count;

                // Check if center moved significantly
                if (Math.Abs(oldL - cluster.Lab_L) > 0.1 ||
                    Math.Abs(oldA - cluster.Lab_A) > 0.1 ||
                    Math.Abs(oldB - cluster.Lab_B) > 0.1) {
                    centersChanged = true;
                }
            }

            // Converged - stop early
            if (!centersChanged) {
                Console.WriteLine($"K-means converged after {iteration + 1} iterations");
                break;
            }
        }

        // Sort by pixel count (most common colors first)
        return clusters.OrderByDescending(c => c.PixelCount).ToList();
    }

    private byte[] ApplyQuantization(Image<Rgb24> image, List<ColorCluster> clusters) {
        using var quantizedImage = image.Clone();

        quantizedImage.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++) {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++) {
                    var pixel = row[x];
                    var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);

                    // Find nearest cluster
                    var nearestCluster = clusters
                        .OrderBy(c => ColorConverter.CalculateLabDistance(l, a, b, c.Lab_L, c.Lab_A, c.Lab_B))
                        .First();

                    // Replace with cluster color
                    row[x] = new Rgb24(nearestCluster.R, nearestCluster.G, nearestCluster.B);
                }
            }
        });

        // Convert to byte array
        using var ms = new MemoryStream();
        quantizedImage.SaveAsPng(ms);
        return ms.ToArray();
    }

    // Helper classes
    private class LabPixel {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public double Lab_L { get; set; }
        public double Lab_A { get; set; }
        public double Lab_B { get; set; }
    }

    private class ColorCluster {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public double Lab_L { get; set; }
        public double Lab_A { get; set; }
        public double Lab_B { get; set; }
        public int PixelCount { get; set; }
        public List<LabPixel> AssignedPixels { get; set; } = new();
    }
}