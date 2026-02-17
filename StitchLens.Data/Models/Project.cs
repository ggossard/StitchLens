using System;

namespace StitchLens.Data.Models;

public class Project
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = "Untitled Pattern";
    public string OriginalImagePath { get; set; } = string.Empty;
    public string? ProcessedImagePath { get; set; }
    public CraftType CraftType { get; set; } = CraftType.Needlepoint;  // Default to needlepoint

    // Canvas settings
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent"; // Tent, Basketweave

    // Yarn selection
    public int? YarnBrandId { get; set; }
    public string? PaletteJson { get; set; } // Stores matched yarn colors
    public bool Public { get; set; }
    public string? Tags { get; set; }

    // Output
    public string? PdfPath { get; set; }
    public int Downloads { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
    public YarnBrand? YarnBrand { get; set; }
}
