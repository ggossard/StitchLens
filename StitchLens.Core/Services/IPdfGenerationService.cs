using StitchLens.Data.Models;
namespace StitchLens.Core.Services;
public interface IPdfGenerationService {
    Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data);
}
public class PatternPdfData {
    public string Title { get; set; } = "Needlepoint Pattern";
    public int MeshCount { get; set; }
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int WidthStitches { get; set; }
    public int HeightStitches { get; set; }
    public string StitchType { get; set; } = "Tent";
    public byte[] QuantizedImageData { get; set; } = Array.Empty<byte>();
    public List<YarnMatch> YarnMatches { get; set; } = new();
    public string YarnBrand { get; set; } = "DMC";
    public StitchGrid? StitchGrid { get; set; }
    public bool UseColoredGrid { get; set; } = false;
}
public class StitchGrid {
    public int Width { get; set; }
    public int Height { get; set; }
    public StitchCell[,] Cells { get; set; } = new StitchCell[0, 0];
}
public class StitchCell {
    public int YarnMatchIndex { get; set; }
    public string Symbol { get; set; } = "";
    public string HexColor { get; set; } = "";
    public bool IsTransparent { get; set; } = false; // NEW: Marks cells that should be left unstitched
}