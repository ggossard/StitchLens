using StitchLens.Core.Services;
using StitchLens.Data.Models;

namespace StitchLens.Web.Models;

public class PreviewViewModel {
    public Project Project { get; set; } = null!;
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;
    public List<YarnMatch>? YarnMatches { get; set; }
    public List<ColorInfo>? UnmatchedColors { get; set; }
    public bool HasYarnMatching => YarnMatches != null && YarnMatches.Any();

    public int PatternsCreatedThisMonth { get; set; }
    public int PatternCreationQuota { get; set; }
    public bool QuotaExceeded => CurrentTier != SubscriptionTier.PayAsYouGo && PatternsCreatedThisMonth >= PatternCreationQuota;
    public SubscriptionTier CurrentTier { get; set; }
    public bool HasPaidForPattern { get; set; } = true;

    public int TotalStitches {
        get {
            if (YarnMatches != null && YarnMatches.Any()) {
                return YarnMatches.Sum(m => m.StitchCount);
            }

            if (UnmatchedColors != null && UnmatchedColors.Any()) {
                return UnmatchedColors.Sum(c => c.PixelCount);
            }

            return Project.WidthInches > 0 && Project.HeightInches > 0
                ? (int)(Project.WidthInches * Project.MeshCount * Project.HeightInches * Project.MeshCount)
                : 0;
        }
    }

    // Totals now allow fractional yards and skeins
    public double TotalYardsNeeded => YarnMatches?.Sum(m => m.YardsNeeded) ?? 0.0;
    public double TotalSkeinsNeeded => YarnMatches?.Sum(m => m.EstimatedSkeins) ?? 0.0;

    // Integer skeins (rounded up to whole skeins per color)
    public int TotalSkeinsRoundedUp => YarnMatches?.Sum(m => (int)Math.Ceiling(m.EstimatedSkeins)) ?? 0;
}
