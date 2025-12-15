using System.Collections.Generic;

namespace StitchLens.Data.Models;

public class YarnBrand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;
    public bool IsActive { get; set; } = true;
    public int YardsPerSkein { get; set; } = 8;
    public decimal YardsPerStitch { get; set; } = 0.5m; // Default for needlepoint

    public ICollection<YarnColor> Colors { get; set; } = new List<YarnColor>();
}