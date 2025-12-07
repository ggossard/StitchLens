using System.Collections.Generic;

namespace StitchLens.Data.Models;

public class YarnBrand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int YardsPerSkein { get; set; } = 8;

    public ICollection<YarnColor> Colors { get; set; } = new List<YarnColor>();
}