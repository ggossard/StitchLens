using StitchLens.Core.Services;
using StitchLens.Data.Models;

namespace StitchLens.Web.Models;

public class PreviewViewModel {
    public Project Project { get; set; } = null!;
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;
    public List<YarnMatch>? YarnMatches { get; set; }
    public List<ColorInfo>? UnmatchedColors { get; set; }
    public bool HasYarnMatching => YarnMatches != null && YarnMatches.Any();

    public int DownloadsUsed { get; set; }
    public int DownloadLimit { get; set; }
    public bool QuotaExceeded => DownloadsUsed >= DownloadLimit;
    public SubscriptionTier CurrentTier { get; set; }

    public int TotalStitches => Project.WidthInches > 0 && Project.HeightInches > 0
        ? (int)(Project.WidthInches * Project.MeshCount * Project.HeightInches * Project.MeshCount)
        : 0;

    public int TotalYardsNeeded => YarnMatches?.Sum(m => m.YardsNeeded) ?? 0;
    public int TotalSkeinsNeeded => YarnMatches?.Sum(m => m.EstimatedSkeins) ?? 0;
}