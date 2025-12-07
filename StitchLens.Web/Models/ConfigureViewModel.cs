using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StitchLens.Web.Models;

public class ConfigureViewModel
{
    public int ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    // Title property with validation
    [Required(ErrorMessage = "Please enter a project title")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    [Display(Name = "Project Title")]
    public string Title { get; set; } = "Untitled Pattern";

    // Canvas settings
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent";

    // Yarn brand selection
    public int? YarnBrandId { get; set; }
    public List<SelectListItem> YarnBrands { get; set; } = new();

    // Available options
    public List<SelectListItem> MeshCountOptions => new()
    {
        new SelectListItem("10 mesh (large stitches)", "10"),
        new SelectListItem("12 mesh", "12"),
        new SelectListItem("14 mesh (standard)", "14", true),
        new SelectListItem("16 mesh", "16"),
        new SelectListItem("18 mesh (fine detail)", "18")
    };

    public List<SelectListItem> StitchTypeOptions => new()
    {
        new SelectListItem("Tent Stitch", "Tent", true),
        new SelectListItem("Basketweave", "Basketweave")
    };

    // Calculated properties
    public int WidthStitches => (int)(WidthInches * MeshCount);
    public int HeightStitches => (int)(HeightInches * MeshCount);
}