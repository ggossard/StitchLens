namespace StitchLens.Data.Models;

public class YarnColor
{
    public int Id { get; set; }
    public int YarnBrandId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HexColor { get; set; } = string.Empty;

    // LAB color space values for accurate matching
    public double Lab_L { get; set; }
    public double Lab_A { get; set; }
    public double Lab_B { get; set; }

    public int YardsPerSkein { get; set; } = 8; // Typical for needlepoint yarn

    // Navigation
    public YarnBrand YarnBrand { get; set; } = null!;
}