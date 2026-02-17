using Microsoft.AspNetCore.Mvc.Rendering;
using StitchLens.Data.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace StitchLens.Web.Models;
public class ConfigureViewModel {
    public int ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    // Title property with validation
    [Required(ErrorMessage = "Please enter a project title")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    [Display(Name = "Project Title")]
    public string Title { get; set; } = "Untitled Pattern";

    [Display(Name = "Share this project")]
    public bool Public { get; set; }

    [StringLength(300, ErrorMessage = "Tags cannot exceed 300 characters")]
    [Display(Name = "Tags")]
    public string? Tags { get; set; }

    // Canvas settings
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent";
    // ADDED: Transparency support
    [Display(Name = "Preserve transparent areas")]
    public bool PreserveTransparency { get; set; } = true;
    // Yarn brand selection
    public int? YarnBrandId { get; set; }
    public List<SelectListItem> YarnBrands { get; set; } = new();
    // All brands with craft type info for client-side filtering
    public List<YarnBrandOption> AllYarnBrands { get; set; } = new();
    // Craft type selection
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;
    // Craft-specific options
    public List<SelectListItem> CraftTypeOptions => new()
    {
        new SelectListItem("Needlepoint", "0", CraftType == CraftType.Needlepoint),
        new SelectListItem("Cross-Stitch", "1", CraftType == CraftType.CrossStitch)
    };
    // Available options
    public List<SelectListItem> MeshCountOptions => CraftType == CraftType.Needlepoint
    ? new()
    {
        new SelectListItem("10 mesh (large stitches)", "10"),
        new SelectListItem("13 mesh (standard)", "13"),
        new SelectListItem("14 mesh", "14"),
        new SelectListItem("18 mesh (fine detail)", "18")
    }
    : new()
    {
        new SelectListItem("11 count", "11"),
        new SelectListItem("14 count (standard)", "14", true),
        new SelectListItem("16 count", "16"),
        new SelectListItem("18 count (fine detail)", "18"),
        new SelectListItem("28 count", "28")
    };
    public List<SelectListItem> StitchTypeOptions => CraftType == CraftType.Needlepoint
    ? new()
    {
        new SelectListItem("Tent Stitch", "Tent", true),
        new SelectListItem("Basketweave", "Basketweave"),
        new SelectListItem("Continental", "Continental")
    }
    : new()
    {
        new SelectListItem("Full Cross", "FullCross", true),
        new SelectListItem("Half Cross", "HalfCross"),
        new SelectListItem("Backstitch", "Backstitch")
    };
    // Calculated properties
    public int WidthStitches => (int)(WidthInches * MeshCount);
    public int HeightStitches => (int)(HeightInches * MeshCount);
}
public class YarnBrandOption {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CraftType { get; set; }
}
