namespace StitchLens.Core.Services;

public interface IYarnMatchingService {
    Task<List<YarnMatch>> MatchColorsToYarnAsync(
        List<ColorInfo> palette,
        int yarnBrandId,
        int totalStitchesInPattern);  // Add this parameter
}

public class YarnMatch {
    public int YarnColorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HexColor { get; set; } = string.Empty;
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public double Lab_L { get; set; }
    public double Lab_A { get; set; }
    public double Lab_B { get; set; }
    public int StitchCount { get; set; }
    public double DeltaE { get; set; }
    public int EstimatedSkeins { get; set; }
    public int YardsNeeded { get; set; }
}