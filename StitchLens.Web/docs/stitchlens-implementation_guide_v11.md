---

# StitchLens Phase 1 Implementation Guide

## Prerequisites

- .NET 8 SDK ([download](https://dotnet.microsoft.com/download))
- Visual Studio 2022 or VS Code with C# extension
- SQL Server Express (or PostgreSQL if preferred)
- Git for version control

## Step 1: Create the Project Structure

Open a terminal and run:

```bash
# Create solution folder
mkdir StitchLens
cd StitchLens

# Create the solution
dotnet new sln -n StitchLens

# Create the main web project (MVC)
dotnet new mvc -n StitchLens.Web

# Create a class library for business logic
dotnet new classlib -n StitchLens.Core

# Create a class library for data access
dotnet new classlib -n StitchLens.Data

# Add projects to solution
dotnet sln add StitchLens.Web/StitchLens.Web.csproj
dotnet sln add StitchLens.Core/StitchLens.Core.csproj
dotnet sln add StitchLens.Data/StitchLens.Data.csproj

# Set up project references
cd StitchLens.Web
dotnet add reference ../StitchLens.Core/StitchLens.Core.csproj
dotnet add reference ../StitchLens.Data/StitchLens.Data.csproj
cd ../StitchLens.Core
dotnet add reference ../StitchLens.Data/StitchLens.Data.csproj
cd ..
```

Your folder structure should now look like:
```
StitchLens/
├── StitchLens.sln
├── StitchLens.Web/          # MVC app (Controllers, Views, wwwroot)
├── StitchLens.Core/         # Business logic (Services)
└── StitchLens.Data/         # Database models and EF Core
```

## Step 2: Install Required NuGet Packages

```bash
# In StitchLens.Web/
cd StitchLens.Web
dotnet add package SixLabors.ImageSharp
dotnet add package SixLabors.ImageSharp.Web
dotnet add package QuestPDF
dotnet add package Stripe.net

# In StitchLens.Data/
cd ../StitchLens.Data
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.EntityFrameworkCore.Design

# In StitchLens.Core/
cd ../StitchLens.Core
dotnet add package SixLabors.ImageSharp
```

## Step 3: Create Core Domain Models

Create these files in `StitchLens.Data/Models/`:

**User.cs**
```csharp
namespace StitchLens.Data.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PlanType { get; set; } = "Free"; // Free, Pro
    public DateTime CreatedAt { get; set; }
    
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
```

**Project.cs**
```csharp
namespace StitchLens.Data.Models;

public class Project
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = "Untitled Pattern";
    public string OriginalImagePath { get; set; } = string.Empty;
    public string? ProcessedImagePath { get; set; }
    
    // Canvas settings
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent"; // Tent, Basketweave
    
    // Yarn selection
    public int? YarnBrandId { get; set; }
    public string? PaletteJson { get; set; } // Stores matched yarn colors
    
    // Output
    public string? PdfPath { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public User? User { get; set; }
    public YarnBrand? YarnBrand { get; set; }
}
```

**YarnBrand.cs**
```csharp
namespace StitchLens.Data.Models;

public class YarnBrand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    public ICollection<YarnColor> Colors { get; set; } = new List<YarnColor>();
}
```

**YarnColor.cs**
```csharp
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
```

## Step 4: Create the Database Context

Create `StitchLens.Data/StitchLensDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StitchLens.Data.Models;

namespace StitchLens.Data;

public class StitchLensDbContext : DbContext
{
    public StitchLensDbContext(DbContextOptions<StitchLensDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<YarnBrand> YarnBrands => Set<YarnBrand>();
    public DbSet<YarnColor> YarnColors => Set<YarnColor>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
        });
        
        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.WidthInches).HasPrecision(10, 2);
            entity.Property(e => e.HeightInches).HasPrecision(10, 2);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.YarnBrand)
                .WithMany()
                .HasForeignKey(e => e.YarnBrandId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // YarnBrand configuration
        modelBuilder.Entity<YarnBrand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
        });
        
        // YarnColor configuration
        modelBuilder.Entity<YarnColor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.HexColor).HasMaxLength(7);
            
            entity.HasOne(e => e.YarnBrand)
                .WithMany(b => b.Colors)
                .HasForeignKey(e => e.YarnBrandId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

## Step 5: Configure Database Connection

Update `StitchLens.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StitchLensDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "FileStorage": {
    "UploadPath": "uploads",
    "TempPath": "temp",
    "PdfPath": "patterns"
  }
}
```

Update `StitchLens.Web/Program.cs` to register the database:

```csharp
using Microsoft.EntityFrameworkCore;
using StitchLens.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register database context
builder.Services.AddDbContext<StitchLensDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

## Step 6: Create Initial Migration

```bash
cd StitchLens.Web
dotnet ef migrations add InitialCreate --project ../StitchLens.Data
dotnet ef database update
```

This creates your database with all the tables!

## Step 7: Create Core Service Interfaces

Create `StitchLens.Core/Services/IImageProcessingService.cs`:

```csharp
namespace StitchLens.Core.Services;

public interface IImageProcessingService
{
    Task<ProcessedImage> ProcessUploadAsync(Stream imageStream, CropData? cropData = null);
    Task<string> SaveImageAsync(ProcessedImage image, string fileName);
}

public class ProcessedImage
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "png";
}

public class CropData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
```

Create `StitchLens.Core/Services/IColorQuantizationService.cs`:

```csharp
namespace StitchLens.Core.Services;

public interface IColorQuantizationService
{
    Task<QuantizedResult> QuantizeAsync(byte[] imageData, int maxColors);
}

public class QuantizedResult
{
    public byte[] QuantizedImageData { get; set; } = Array.Empty<byte>();
    public List<ColorInfo> Palette { get; set; } = new();
}

public class ColorInfo
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public double Lab_L { get; set; }
    public double Lab_A { get; set; }
    public double Lab_B { get; set; }
    public int PixelCount { get; set; }
}
```

## Step 8: Implement Basic Image Processing Service

Create `StitchLens.Core/Services/ImageProcessingService.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace StitchLens.Core.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly string _uploadPath;
    
    public ImageProcessingService(string uploadPath)
    {
        _uploadPath = uploadPath;
        Directory.CreateDirectory(_uploadPath);
    }
    
    public async Task<ProcessedImage> ProcessUploadAsync(Stream imageStream, CropData? cropData = null)
    {
        using var image = await Image.LoadAsync(imageStream);
        
        // Apply crop if specified
        if (cropData != null)
        {
            image.Mutate(x => x.Crop(new Rectangle(
                cropData.X,
                cropData.Y,
                cropData.Width,
                cropData.Height
            )));
        }
        
        // Limit max dimensions to prevent huge processing times
        const int maxDimension = 2000;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            var ratio = Math.Min(
                (double)maxDimension / image.Width,
                (double)maxDimension / image.Height
            );
            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);
            
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }
        
        // Convert to byte array
        using var ms = new MemoryStream();
        await image.SaveAsync(ms, new PngEncoder());
        
        return new ProcessedImage
        {
            ImageData = ms.ToArray(),
            Width = image.Width,
            Height = image.Height,
            Format = "png"
        };
    }
    
    public async Task<string> SaveImageAsync(ProcessedImage image, string fileName)
    {
        var filePath = Path.Combine(_uploadPath, fileName);
        await File.WriteAllBytesAsync(filePath, image.ImageData);
        return filePath;
    }
}
```

## Step 9: Create Your First Controller

Create `StitchLens.Web/Controllers/PatternController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;

namespace StitchLens.Web.Controllers;

public class PatternController : Controller
{
    private readonly StitchLensDbContext _context;
    private readonly IImageProcessingService _imageService;
    
    public PatternController(
        StitchLensDbContext context,
        IImageProcessingService imageService)
    {
        _context = context;
        _imageService = imageService;
    }
    
    // Step 1: Show upload form
    public IActionResult Upload()
    {
        return View();
    }
    
    // Step 2: Process uploaded image
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select an image file.");
            return View();
        }
        
        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
        {
            ModelState.AddModelError("", "Only JPG and PNG files are allowed.");
            return View();
        }
        
        // Process the image
        using var stream = imageFile.OpenReadStream();
        var processed = await _imageService.ProcessUploadAsync(stream);
        
        // Save to disk with unique filename
        var fileName = $"{Guid.NewGuid()}.png";
        var filePath = await _imageService.SaveImageAsync(processed, fileName);
        
        // Create project record
        var project = new Project
        {
            OriginalImagePath = filePath,
            CreatedAt = DateTime.UtcNow,
            WidthInches = processed.Width / 96m, // Estimate based on typical screen DPI
            HeightInches = processed.Height / 96m
        };
        
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        
        // Redirect to configuration page
        return RedirectToAction("Configure", new { id = project.Id });
    }
    
    // Step 3: Show settings form
    public async Task<IActionResult> Configure(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();
            
        return View(project);
    }
}
```

## Step 10: Register Services in Program.cs

Update `StitchLens.Web/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.Services;
using StitchLens.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register database context
builder.Services.AddDbContext<StitchLensDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application services
var uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton<IImageProcessingService>(
    new ImageProcessingService(uploadPath));

var app = builder.Build();

// ... rest of the configuration
```

## Step 11: Create Your First View

Create `StitchLens.Web/Views/Pattern/Upload.cshtml`:

```html
@{
    ViewData["Title"] = "Upload Photo";
}

<div class="container mt-5">
    <div class="row justify-content-center">
        <div class="col-md-8">
            <h2>Create Your Needlepoint Pattern</h2>
            <p class="lead">Upload a photo to get started</p>
            
            <form method="post" enctype="multipart/form-data" asp-action="Upload">
                <div class="mb-3">
                    <label for="imageFile" class="form-label">Choose an image</label>
                    <input type="file" 
                           class="form-control" 
                           id="imageFile" 
                           name="imageFile" 
                           accept="image/jpeg,image/png,image/jpg"
                           required>
                    <div class="form-text">
                        JPG or PNG format, recommended size: 500-2000 pixels
                    </div>
                </div>
                
                <div class="mb-3">
                    <img id="preview" 
                         src="#" 
                         alt="Preview" 
                         style="max-width: 100%; display: none;" 
                         class="img-thumbnail">
                </div>
                
                <div asp-validation-summary="All" class="text-danger"></div>
                
                <button type="submit" class="btn btn-primary btn-lg">
                    Continue to Settings
                </button>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <script>
        document.getElementById('imageFile').addEventListener('change', function(e) {
            const file = e.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = function(event) {
                    const preview = document.getElementById('preview');
                    preview.src = event.target.result;
                    preview.style.display = 'block';
                };
                reader.readAsDataURL(file);
            }
        });
    </script>
}
```

## Step 12: Test Your Setup

```bash
cd StitchLens.Web
dotnet run
```

Navigate to `https://localhost:5001/Pattern/Upload`

You should see your upload form! Try uploading an image - it will save to the database and redirect to the Configure page (which we'll build next).

## What We've Accomplished

✅ Project structure with proper separation of concerns  
✅ Database with Entity Framework Core  
✅ Basic image upload and processing  
✅ File storage system  
✅ First working controller and view  
✅ Service layer foundation  

## Phase 2: Configure Page

Now let's build the Configure page where users set their pattern parameters.

### Step 13: Create ViewModel for Configure Page

Create `StitchLens.Web/Models/ConfigureViewModel.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StitchLens.Web.Models;

public class ConfigureViewModel
{
    public int ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    
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
```

### Step 14: Update PatternController - Configure Action

Update `StitchLens.Web/Controllers/PatternController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Models;

namespace StitchLens.Web.Controllers;

public class PatternController : Controller
{
    private readonly StitchLensDbContext _context;
    private readonly IImageProcessingService _imageService;
    
    public PatternController(
        StitchLensDbContext context,
        IImageProcessingService imageService)
    {
        _context = context;
        _imageService = imageService;
    }
    
    // Step 1: Show upload form
    public IActionResult Upload()
    {
        return View();
    }
    
    // Step 2: Process uploaded image
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select an image file.");
            return View();
        }
        
        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
        {
            ModelState.AddModelError("", "Only JPG and PNG files are allowed.");
            return View();
        }
        
        // Process the image
        using var stream = imageFile.OpenReadStream();
        var processed = await _imageService.ProcessUploadAsync(stream);
        
        // Save to disk with unique filename
        var fileName = $"{Guid.NewGuid()}.png";
        var filePath = await _imageService.SaveImageAsync(processed, fileName);
        
        // Create project record
        var project = new Project
        {
            OriginalImagePath = filePath,
            CreatedAt = DateTime.UtcNow,
            WidthInches = processed.Width / 96m, // Estimate based on typical screen DPI
            HeightInches = processed.Height / 96m
        };
        
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        
        // Redirect to configuration page
        return RedirectToAction("Configure", new { id = project.Id });
    }
    
    // Step 3: Show settings form
    public async Task<IActionResult> Configure(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();
        
        // Get available yarn brands
        var yarnBrands = await _context.YarnBrands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToListAsync();
        
        var viewModel = new ConfigureViewModel
        {
            ProjectId = project.Id,
            ImageUrl = $"/uploads/{Path.GetFileName(project.OriginalImagePath)}",
            MeshCount = project.MeshCount,
            WidthInches = project.WidthInches,
            HeightInches = project.HeightInches,
            MaxColors = project.MaxColors,
            StitchType = project.StitchType,
            YarnBrandId = project.YarnBrandId,
            YarnBrands = yarnBrands
        };
        
        return View(viewModel);
    }
    
    // Step 4: Process configuration and start pattern generation
    [HttpPost]
    public async Task<IActionResult> Configure(ConfigureViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Reload yarn brands if validation fails
            model.YarnBrands = await _context.YarnBrands
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();
            
            return View(model);
        }
        
        // Update project with settings
        var project = await _context.Projects.FindAsync(model.ProjectId);
        if (project == null)
            return NotFound();
        
        project.MeshCount = model.MeshCount;
        project.WidthInches = model.WidthInches;
        project.HeightInches = model.HeightInches;
        project.MaxColors = model.MaxColors;
        project.StitchType = model.StitchType;
        project.YarnBrandId = model.YarnBrandId;
        
        await _context.SaveChangesAsync();
        
        // TODO: Start pattern generation (next phase)
        // For now, just redirect to a preview page
        return RedirectToAction("Preview", new { id = project.Id });
    }
    
    // Placeholder for preview page
    public async Task<IActionResult> Preview(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();
        
        return View(project);
    }
}
```

### Step 15: Create Configure View

Create `StitchLens.Web/Views/Pattern/Configure.cshtml`:

```html
@model StitchLens.Web.Models.ConfigureViewModel
@{
    ViewData["Title"] = "Configure Pattern";
}

<div class="container mt-4">
    <h2>Configure Your Pattern</h2>
    <p class="lead">Set your canvas dimensions and color preferences</p>
    
    <div class="row">
        <!-- Left side: Image preview -->
        <div class="col-md-6">
            <div class="card">
                <div class="card-header">
                    <h5>Your Image</h5>
                </div>
                <div class="card-body text-center">
                    <img src="@Model.ImageUrl" 
                         alt="Uploaded image" 
                         class="img-fluid"
                         style="max-height: 400px;">
                </div>
            </div>
            
            <div class="card mt-3">
                <div class="card-body">
                    <h6>Pattern Size</h6>
                    <p class="mb-1">
                        <strong>Canvas:</strong> 
                        <span id="canvasSize">@Model.WidthInches.ToString("F1")" × @Model.HeightInches.ToString("F1")"</span>
                    </p>
                    <p class="mb-0">
                        <strong>Stitches:</strong> 
                        <span id="stitchCount">@Model.WidthStitches × @Model.HeightStitches</span>
                        (<span id="totalStitches">@((Model.WidthStitches * Model.HeightStitches).ToString("N0"))</span> total)
                    </p>
                </div>
            </div>
        </div>
        
        <!-- Right side: Settings form -->
        <div class="col-md-6">
            <form asp-action="Configure" method="post" id="configureForm">
                <input type="hidden" asp-for="ProjectId" />
                <input type="hidden" asp-for="ImageUrl" />
                
                <div class="card">
                    <div class="card-header">
                        <h5>Canvas Settings</h5>
                    </div>
                    <div class="card-body">
                        <!-- Mesh Count -->
                        <div class="mb-3">
                            <label asp-for="MeshCount" class="form-label">Mesh Count</label>
                            <select asp-for="MeshCount" 
                                    asp-items="Model.MeshCountOptions"
                                    class="form-select"
                                    id="meshCount">
                            </select>
                            <div class="form-text">
                                Higher mesh = smaller stitches and more detail
                            </div>
                        </div>
                        
                        <!-- Dimensions -->
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <label asp-for="WidthInches" class="form-label">Width (inches)</label>
                                <input asp-for="WidthInches" 
                                       type="number" 
                                       step="0.1" 
                                       min="1" 
                                       max="36"
                                       class="form-control dimension-input"
                                       id="widthInches">
                            </div>
                            <div class="col-md-6 mb-3">
                                <label asp-for="HeightInches" class="form-label">Height (inches)</label>
                                <input asp-for="HeightInches" 
                                       type="number" 
                                       step="0.1" 
                                       min="1" 
                                       max="36"
                                       class="form-control dimension-input"
                                       id="heightInches">
                            </div>
                        </div>
                        
                        <!-- Stitch Type -->
                        <div class="mb-3">
                            <label asp-for="StitchType" class="form-label">Stitch Type</label>
                            <select asp-for="StitchType" 
                                    asp-items="Model.StitchTypeOptions"
                                    class="form-select">
                            </select>
                        </div>
                        
                        <!-- Max Colors -->
                        <div class="mb-3">
                            <label asp-for="MaxColors" class="form-label">
                                Maximum Colors: <span id="colorDisplay">@Model.MaxColors</span>
                            </label>
                            <input asp-for="MaxColors" 
                                   type="range" 
                                   min="10" 
                                   max="60" 
                                   step="5"
                                   class="form-range"
                                   id="maxColors">
                            <div class="form-text">
                                Fewer colors = simpler pattern, more colors = more detail
                            </div>
                        </div>
                        
                        <!-- Yarn Brand -->
                        <div class="mb-3">
                            <label asp-for="YarnBrandId" class="form-label">Yarn Brand</label>
                            <select asp-for="YarnBrandId" 
                                    asp-items="Model.YarnBrands"
                                    class="form-select">
                                <option value="">-- Select a brand --</option>
                            </select>
                            <div class="form-text">
                                Choose your preferred yarn brand for color matching
                            </div>
                        </div>
                    </div>
                </div>
                
                <div asp-validation-summary="All" class="text-danger mt-3"></div>
                
                <div class="d-grid gap-2 mt-3">
                    <button type="submit" class="btn btn-primary btn-lg">
                        Generate Pattern
                    </button>
                    <a asp-action="Upload" class="btn btn-outline-secondary">
                        Start Over
                    </a>
                </div>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <script>
        // Update color count display as slider moves
        document.getElementById('maxColors').addEventListener('input', function(e) {
            document.getElementById('colorDisplay').textContent = e.target.value;
        });
        
        // Update stitch count calculations when dimensions or mesh change
        function updateCalculations() {
            const meshCount = parseInt(document.getElementById('meshCount').value);
            const widthInches = parseFloat(document.getElementById('widthInches').value);
            const heightInches = parseFloat(document.getElementById('heightInches').value);
            
            if (meshCount && widthInches && heightInches) {
                const widthStitches = Math.round(widthInches * meshCount);
                const heightStitches = Math.round(heightInches * meshCount);
                const totalStitches = widthStitches * heightStitches;
                
                document.getElementById('canvasSize').textContent = 
                    widthInches.toFixed(1) + '" × ' + heightInches.toFixed(1) + '"';
                document.getElementById('stitchCount').textContent = 
                    widthStitches + ' × ' + heightStitches;
                document.getElementById('totalStitches').textContent = 
                    totalStitches.toLocaleString();
            }
        }
        
        document.getElementById('meshCount').addEventListener('change', updateCalculations);
        document.getElementById('widthInches').addEventListener('input', updateCalculations);
        document.getElementById('heightInches').addEventListener('input', updateCalculations);
    </script>
}
```

### Step 16: Configure Static Files to Serve Uploads

Update `StitchLens.Web/Program.cs` to serve the uploads folder:

```csharp
// After app.UseStaticFiles(); add:
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});
```

You'll also need to add this using at the top:
```csharp
using Microsoft.Extensions.FileProviders;
```

### Step 17: Seed Sample Yarn Brands

Let's add a few yarn brands so the dropdown works. Create a new file `StitchLens.Data/DbInitializer.cs`:

```csharp
using StitchLens.Data.Models;

namespace StitchLens.Data;

public static class DbInitializer
{
    public static void Initialize(StitchLensDbContext context)
    {
        // Ensure database is created
        context.Database.EnsureCreated();
        
        // Check if brands already exist
        if (context.YarnBrands.Any())
            return; // Already seeded
        
        // Add sample yarn brands
        var brands = new[]
        {
            new YarnBrand { Name = "DMC", Country = "France", IsActive = true },
            new YarnBrand { Name = "Appleton", Country = "UK", IsActive = true },
            new YarnBrand { Name = "Paternayan", Country = "USA", IsActive = true }
        };
        
        context.YarnBrands.AddRange(brands);
        context.SaveChanges();
    }
}
```

Then call it from `Program.cs` after the app is built:

```csharp
var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StitchLensDbContext>();
    DbInitializer.Initialize(context);
}

// Continue with rest of configuration...
```

### Step 18: Create Placeholder Preview Page

Create `StitchLens.Web/Views/Pattern/Preview.cshtml`:

```html
@model StitchLens.Data.Models.Project
@{
    ViewData["Title"] = "Pattern Preview";
}

<div class="container mt-4">
    <h2>Pattern Preview</h2>
    <div class="alert alert-info">
        <h5>Coming Soon!</h5>
        <p>Your pattern is being generated with these settings:</p>
        <ul>
            <li>Mesh Count: @Model.MeshCount</li>
            <li>Size: @Model.WidthInches.ToString("F1")" × @Model.HeightInches.ToString("F1")"</li>
            <li>Max Colors: @Model.MaxColors</li>
            <li>Stitch Type: @Model.StitchType</li>
        </ul>
        <p class="mb-0">
            Next phase: We'll implement the color quantization and yarn matching!
        </p>
    </div>
    
    <a asp-action="Configure" asp-route-id="@Model.Id" class="btn btn-secondary">
        Back to Settings
    </a>
</div>
```

### Test Your Configure Page

Run the application:
```bash
cd StitchLens.Web
dotnet run
```

Now you should be able to:
1. Upload an image
2. See the Configure page with all settings
3. Adjust mesh count, dimensions, colors
4. See live calculations update
5. Submit (goes to placeholder Preview page)

You now have the complete UI for pattern configuration! Next we'll implement the actual pattern generation logic.

---

## Phase 3: Add Image Cropping

Let's add the ability to crop uploaded images before processing.

### Step 19: Install Cropper.js via CDN

We'll use Cropper.js from a CDN. Update `StitchLens.Web/Views/Shared/_Layout.cshtml` to include it in the head section:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - StitchLens</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.css" />
    <link rel="stylesheet" href="~/StitchLens.Web.styles.css" asp-append-version="true" />
</head>
<body>
    <!-- rest of layout -->
    
    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### Step 20: Update Upload View with Cropper

Replace `StitchLens.Web/Views/Pattern/Upload.cshtml` with this enhanced version:

```html
@{
    ViewData["Title"] = "Upload Photo";
}

<div class="container mt-5">
    <div class="row justify-content-center">
        <div class="col-md-10">
            <h2>Create Your Needlepoint Pattern</h2>
            <p class="lead">Upload a photo to get started</p>
            
            <form method="post" enctype="multipart/form-data" asp-action="ProcessUpload" id="uploadForm">
                <!-- Step 1: File Selection -->
                <div id="uploadSection">
                    <div class="card">
                        <div class="card-body">
                            <div class="mb-3">
                                <label for="imageFile" class="form-label">Choose an image</label>
                                <input type="file" 
                                       class="form-control" 
                                       id="imageFile" 
                                       name="imageFile" 
                                       accept="image/jpeg,image/png,image/jpg"
                                       required>
                                <div class="form-text">
                                    JPG or PNG format, recommended size: 500-2000 pixels
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Step 2: Cropping Interface (hidden initially) -->
                <div id="cropSection" style="display: none;">
                    <div class="card">
                        <div class="card-header">
                            <h5>Crop Your Image</h5>
                            <small class="text-muted">Drag to select the area you want to stitch</small>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-9">
                                    <div style="max-height: 500px; overflow: hidden;">
                                        <img id="cropImage" style="max-width: 100%;">
                                    </div>
                                </div>
                                <div class="col-md-3">
                                    <h6>Crop Tools</h6>
                                    <div class="d-grid gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.reset()">
                                            Reset
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.clear()">
                                            Clear
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.setDragMode('move')">
                                            Move Image
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.setDragMode('crop')">
                                            Crop Area
                                        </button>
                                    </div>
                                    
                                    <hr>
                                    
                                    <h6 class="mt-3">Aspect Ratio</h6>
                                    <div class="d-grid gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(NaN)">
                                            Free
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(1)">
                                            Square (1:1)
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(4/3)">
                                            4:3
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(16/9)">
                                            16:9
                                        </button>
                                    </div>
                                    
                                    <hr>
                                    
                                    <div class="mt-3">
                                        <small class="text-muted">
                                            <strong>Crop Size:</strong><br>
                                            <span id="cropInfo">Select an area</span>
                                        </small>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Hidden fields to store crop data -->
                    <input type="hidden" name="cropX" id="cropX">
                    <input type="hidden" name="cropY" id="cropY">
                    <input type="hidden" name="cropWidth" id="cropWidth">
                    <input type="hidden" name="cropHeight" id="cropHeight">
                    <input type="hidden" name="originalWidth" id="originalWidth">
                    <input type="hidden" name="originalHeight" id="originalHeight">
                </div>
                
                <div asp-validation-summary="All" class="text-danger mt-3"></div>
                
                <!-- Action Buttons -->
                <div class="d-grid gap-2 mt-3">
                    <button type="button" id="cancelCrop" class="btn btn-secondary" style="display: none;" onclick="cancelCrop()">
                        Choose Different Image
                    </button>
                    <button type="submit" id="continueBtn" class="btn btn-primary btn-lg" style="display: none;">
                        Continue to Settings
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>

@section Scripts {
    <script>
        let cropper = null;
        
        // Handle file selection
        document.getElementById('imageFile').addEventListener('change', function(e) {
            const file = e.target.files[0];
            if (!file) return;
            
            // Validate file size (max 10MB)
            if (file.size > 10 * 1024 * 1024) {
                alert('File is too large. Please choose an image under 10MB.');
                this.value = '';
                return;
            }
            
            const reader = new FileReader();
            reader.onload = function(event) {
                // Show crop section
                document.getElementById('uploadSection').style.display = 'none';
                document.getElementById('cropSection').style.display = 'block';
                document.getElementById('cancelCrop').style.display = 'block';
                document.getElementById('continueBtn').style.display = 'block';
                
                // Set image and initialize cropper
                const image = document.getElementById('cropImage');
                image.src = event.target.result;
                
                // Destroy previous cropper if exists
                if (cropper) {
                    cropper.destroy();
                }
                
                // Initialize Cropper.js
                cropper = new Cropper(image, {
                    viewMode: 1,
                    dragMode: 'move',
                    aspectRatio: NaN, // Free aspect ratio
                    autoCropArea: 0.8,
                    restore: false,
                    guides: true,
                    center: true,
                    highlight: false,
                    cropBoxMovable: true,
                    cropBoxResizable: true,
                    toggleDragModeOnDblclick: false,
                    ready: function() {
                        // Store original dimensions
                        const imageData = cropper.getImageData();
                        document.getElementById('originalWidth').value = imageData.naturalWidth;
                        document.getElementById('originalHeight').value = imageData.naturalHeight;
                    },
                    crop: function(event) {
                        // Update crop info display
                        const width = Math.round(event.detail.width);
                        const height = Math.round(event.detail.height);
                        document.getElementById('cropInfo').innerHTML = 
                            `${width} × ${height}px`;
                        
                        // Store crop data for form submission
                        document.getElementById('cropX').value = Math.round(event.detail.x);
                        document.getElementById('cropY').value = Math.round(event.detail.y);
                        document.getElementById('cropWidth').value = width;
                        document.getElementById('cropHeight').value = height;
                    }
                });
            };
            
            reader.readAsDataURL(file);
        });
        
        // Cancel crop and go back to file selection
        function cancelCrop() {
            if (cropper) {
                cropper.destroy();
                cropper = null;
            }
            
            document.getElementById('imageFile').value = '';
            document.getElementById('uploadSection').style.display = 'block';
            document.getElementById('cropSection').style.display = 'none';
            document.getElementById('cancelCrop').style.display = 'none';
            document.getElementById('continueBtn').style.display = 'none';
        }
        
        // Validate crop data before form submission
        document.getElementById('uploadForm').addEventListener('submit', function(e) {
            if (!cropper) {
                e.preventDefault();
                alert('Please select and crop an image first.');
                return false;
            }
            
            // Ensure crop data is set
            const cropWidth = document.getElementById('cropWidth').value;
            if (!cropWidth || cropWidth === '0') {
                e.preventDefault();
                alert('Please select a crop area.');
                return false;
            }
        });
    </script>
}
```

### Step 21: Update PatternController to Handle Crop Data

Update the `Upload` POST action in `PatternController.cs`:

```csharp
// Step 2: Process uploaded image with crop data
[HttpPost]
[Route("Pattern/ProcessUpload")]
public async Task<IActionResult> ProcessUpload(
    IFormFile imageFile,
    int cropX,
    int cropY,
    int cropWidth,
    int cropHeight,
    int originalWidth,
    int originalHeight)
{
    if (imageFile == null || imageFile.Length == 0)
    {
        ModelState.AddModelError("", "Please select an image file.");
        return View("Upload");
    }
    
    // Validate file type
    var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
    if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
    {
        ModelState.AddModelError("", "Only JPG and PNG files are allowed.");
        return View("Upload");
    }
    
    // Validate crop dimensions
    if (cropWidth <= 0 || cropHeight <= 0)
    {
        ModelState.AddModelError("", "Invalid crop dimensions.");
        return View("Upload");
    }
    
    // Create crop data object
    CropData? cropData = null;
    if (cropWidth > 0 && cropHeight > 0)
    {
        cropData = new CropData
        {
            X = cropX,
            Y = cropY,
            Width = cropWidth,
            Height = cropHeight
        };
    }
    
    // Process the image with cropping
    using var stream = imageFile.OpenReadStream();
    var processed = await _imageService.ProcessUploadAsync(stream, cropData);
    
    // Save to disk with unique filename
    var fileName = $"{Guid.NewGuid()}.png";
    var filePath = await _imageService.SaveImageAsync(processed, fileName);
    
    // Create project record
    var project = new Project
    {
        OriginalImagePath = filePath,
        CreatedAt = DateTime.UtcNow,
        WidthInches = processed.Width / 96m, // Estimate based on typical screen DPI
        HeightInches = processed.Height / 96m
    };
    
    _context.Projects.Add(project);
    await _context.SaveChangesAsync();
    
    // Redirect to configuration page
    return RedirectToAction("Configure", new { id = project.Id });
}
```

Also add the Route attribute at the top of the class if not already there:

```csharp
using Microsoft.AspNetCore.Mvc;
```

### Step 22: Test Image Cropping

Run your application:

```bash
cd StitchLens.Web
dotnet run
```

Now when you upload an image:
1. ✅ File selection appears first
2. ✅ After selecting, cropping interface appears
3. ✅ You can drag to select crop area
4. ✅ Aspect ratio buttons to constrain proportions
5. ✅ Reset/Clear tools
6. ✅ Live crop dimensions displayed
7. ✅ "Continue to Settings" processes the cropped image

The cropped image is processed server-side by ImageSharp, so the crop is applied before saving and configuration.

### Troubleshooting

If the cropper doesn't appear:
- Check browser console for JavaScript errors
- Verify Cropper.js loaded (check Network tab)
- Make sure jQuery loads before Cropper.js

If crops aren't working:
- Verify the hidden form fields have values before submit
- Check that `CropData` class exists in your Services namespace
- Ensure `ProcessUploadAsync` handles null cropData gracefully

You now have full image upload with cropping! Ready for the pattern generation engine?

---

## Phase 4: Color Quantization Engine

Now let's build the core pattern generation - reducing images to limited color palettes.

### Step 23: Create Color Space Conversion Utilities

Create `StitchLens.Core/ColorScience/ColorConverter.cs`:

```csharp
namespace StitchLens.Core.ColorScience;

public static class ColorConverter
{
    /// <summary>
    /// Convert RGB to LAB color space (D65 illuminant, 2° observer)
    /// LAB is perceptually uniform - better for color matching than RGB
    /// </summary>
    public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
    {
        // First convert RGB to XYZ
        var (x, y, z) = RgbToXyz(r, g, b);
        
        // Then XYZ to LAB
        return XyzToLab(x, y, z);
    }
    
    /// <summary>
    /// Convert LAB back to RGB
    /// </summary>
    public static (byte R, byte G, byte B) LabToRgb(double l, double a, double b)
    {
        // LAB to XYZ
        var (x, y, z) = LabToXyz(l, a, b);
        
        // XYZ to RGB
        return XyzToRgb(x, y, z);
    }
    
    private static (double X, double Y, double Z) RgbToXyz(byte r, byte g, byte b)
    {
        // Normalize RGB to 0-1
        double rLinear = r / 255.0;
        double gLinear = g / 255.0;
        double bLinear = b / 255.0;
        
        // Apply gamma correction (sRGB)
        rLinear = rLinear > 0.04045 ? Math.Pow((rLinear + 0.055) / 1.055, 2.4) : rLinear / 12.92;
        gLinear = gLinear > 0.04045 ? Math.Pow((gLinear + 0.055) / 1.055, 2.4) : gLinear / 12.92;
        bLinear = bLinear > 0.04045 ? Math.Pow((bLinear + 0.055) / 1.055, 2.4) : bLinear / 12.92;
        
        // Convert to XYZ using D65 illuminant matrix
        double x = rLinear * 0.4124564 + gLinear * 0.3575761 + bLinear * 0.1804375;
        double y = rLinear * 0.2126729 + gLinear * 0.7151522 + bLinear * 0.0721750;
        double z = rLinear * 0.0193339 + gLinear * 0.1191920 + bLinear * 0.9503041;
        
        return (x * 100, y * 100, z * 100);
    }
    
    private static (double L, double A, double B) XyzToLab(double x, double y, double z)
    {
        // D65 reference white point
        const double refX = 95.047;
        const double refY = 100.000;
        const double refZ = 108.883;
        
        double xr = x / refX;
        double yr = y / refY;
        double zr = z / refZ;
        
        // Apply LAB conversion function
        xr = xr > 0.008856 ? Math.Pow(xr, 1.0 / 3.0) : (7.787 * xr + 16.0 / 116.0);
        yr = yr > 0.008856 ? Math.Pow(yr, 1.0 / 3.0) : (7.787 * yr + 16.0 / 116.0);
        zr = zr > 0.008856 ? Math.Pow(zr, 1.0 / 3.0) : (7.787 * zr + 16.0 / 116.0);
        
        double l = (116.0 * yr) - 16.0;
        double a = 500.0 * (xr - yr);
        double b = 200.0 * (yr - zr);
        
        return (l, a, b);
    }
    
    private static (double X, double Y, double Z) LabToXyz(double l, double a, double b)
    {
        const double refX = 95.047;
        const double refY = 100.000;
        const double refZ = 108.883;
        
        double fy = (l + 16.0) / 116.0;
        double fx = a / 500.0 + fy;
        double fz = fy - b / 200.0;
        
        double xr = fx * fx * fx > 0.008856 ? fx * fx * fx : (fx - 16.0 / 116.0) / 7.787;
        double yr = fy * fy * fy > 0.008856 ? fy * fy * fy : (fy - 16.0 / 116.0) / 7.787;
        double zr = fz * fz * fz > 0.008856 ? fz * fz * fz : (fz - 16.0 / 116.0) / 7.787;
        
        return (xr * refX, yr * refY, zr * refZ);
    }
    
    private static (byte R, byte G, byte B) XyzToRgb(double x, double y, double z)
    {
        x /= 100.0;
        y /= 100.0;
        z /= 100.0;
        
        // XYZ to linear RGB
        double r = x * 3.2404542 + y * -1.5371385 + z * -0.4985314;
        double g = x * -0.9692660 + y * 1.8760108 + z * 0.0415560;
        double b = x * 0.0556434 + y * -0.2040259 + z * 1.0572252;
        
        // Apply inverse gamma correction (sRGB)
        r = r > 0.0031308 ? 1.055 * Math.Pow(r, 1.0 / 2.4) - 0.055 : 12.92 * r;
        g = g > 0.0031308 ? 1.055 * Math.Pow(g, 1.0 / 2.4) - 0.055 : 12.92 * g;
        b = b > 0.0031308 ? 1.055 * Math.Pow(b, 1.0 / 2.4) - 0.055 : 12.92 * b;
        
        // Clamp to valid range and convert to byte
        byte rByte = (byte)Math.Clamp(r * 255.0, 0, 255);
        byte gByte = (byte)Math.Clamp(g * 255.0, 0, 255);
        byte bByte = (byte)Math.Clamp(b * 255.0, 0, 255);
        
        return (rByte, gByte, bByte);
    }
    
    /// <summary>
    /// Calculate perceptual color difference using simple Euclidean distance in LAB space
    /// For more accuracy, use DeltaE2000 (coming in next phase)
    /// </summary>
    public static double CalculateLabDistance(
        double l1, double a1, double b1,
        double l2, double a2, double b2)
    {
        double dL = l1 - l2;
        double dA = a1 - a2;
        double dB = b1 - b2;
        
        return Math.Sqrt(dL * dL + dA * dA + dB * dB);
    }
}
```

### Step 24: Implement K-means Color Quantization

Update `StitchLens.Core/Services/ColorQuantizationService.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StitchLens.Core.ColorScience;
using Image = SixLabors.ImageSharp.Image;

namespace StitchLens.Core.Services;

public class ColorQuantizationService : IColorQuantizationService
{
    public async Task<QuantizedResult> QuantizeAsync(byte[] imageData, int maxColors)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load<Rgb24>(imageData);
            
            // Step 1: Extract all unique colors from image
            var pixels = ExtractPixels(image);
            
            // Step 2: Run K-means clustering in LAB space
            var clusters = KMeansClustering(pixels, maxColors);
            
            // Step 3: Create quantized image using cluster centers
            var quantizedImageData = ApplyQuantization(image, clusters);
            
            // Step 4: Build palette info
            var palette = clusters.Select(c => new ColorInfo
            {
                R = c.R,
                G = c.G,
                B = c.B,
                Lab_L = c.Lab_L,
                Lab_A = c.Lab_A,
                Lab_B = c.Lab_B,
                PixelCount = c.PixelCount
            }).ToList();
            
            return new QuantizedResult
            {
                QuantizedImageData = quantizedImageData,
                Palette = palette
            };
        });
    }
    
    private List<LabPixel> ExtractPixels(Image<Rgb24> image)
    {
        var pixels = new List<LabPixel>();
        
        for (int y = 0; y < image.Height; y++)
        {
            var row = image.GetPixelRowSpan(y);
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = row[x];
                var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);
                
                pixels.Add(new LabPixel
                {
                    R = pixel.R,
                    G = pixel.G,
                    B = pixel.B,
                    Lab_L = l,
                    Lab_A = a,
                    Lab_B = b
                });
            }
        }
        
        return pixels;
    }
    
    private List<ColorCluster> KMeansClustering(List<LabPixel> pixels, int k, int maxIterations = 20)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Initialize cluster centers randomly from existing pixels
        var clusters = pixels
            .OrderBy(_ => random.Next())
            .Take(k)
            .Select(p => new ColorCluster
            {
                R = p.R,
                G = p.G,
                B = p.B,
                Lab_L = p.Lab_L,
                Lab_A = p.Lab_A,
                Lab_B = p.Lab_B
            })
            .ToList();
        
        // K-means iterations
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Reset cluster assignments
            foreach (var cluster in clusters)
            {
                cluster.AssignedPixels.Clear();
            }
            
            // Assign each pixel to nearest cluster (in LAB space)
            foreach (var pixel in pixels)
            {
                var nearestCluster = clusters
                    .OrderBy(c => ColorConverter.CalculateLabDistance(
                        pixel.Lab_L, pixel.Lab_A, pixel.Lab_B,
                        c.Lab_L, c.Lab_A, c.Lab_B))
                    .First();
                
                nearestCluster.AssignedPixels.Add(pixel);
            }
            
            // Recalculate cluster centers
            bool centersChanged = false;
            foreach (var cluster in clusters)
            {
                if (cluster.AssignedPixels.Count == 0) continue;
                
                var oldL = cluster.Lab_L;
                var oldA = cluster.Lab_A;
                var oldB = cluster.Lab_B;
                
                // Calculate mean LAB values
                cluster.Lab_L = cluster.AssignedPixels.Average(p => p.Lab_L);
                cluster.Lab_A = cluster.AssignedPixels.Average(p => p.Lab_A);
                cluster.Lab_B = cluster.AssignedPixels.Average(p => p.Lab_B);
                
                // Convert back to RGB for display
                var (r, g, b) = ColorConverter.LabToRgb(cluster.Lab_L, cluster.Lab_A, cluster.Lab_B);
                cluster.R = r;
                cluster.G = g;
                cluster.B = b;
                
                cluster.PixelCount = cluster.AssignedPixels.Count;
                
                // Check if center moved significantly
                if (Math.Abs(oldL - cluster.Lab_L) > 0.1 ||
                    Math.Abs(oldA - cluster.Lab_A) > 0.1 ||
                    Math.Abs(oldB - cluster.Lab_B) > 0.1)
                {
                    centersChanged = true;
                }
            }
            
            // Converged - stop early
            if (!centersChanged)
            {
                Console.WriteLine($"K-means converged after {iteration + 1} iterations");
                break;
            }
        }
        
        // Sort by pixel count (most common colors first)
        return clusters.OrderByDescending(c => c.PixelCount).ToList();
    }
    
    private byte[] ApplyQuantization(Image<Rgb24> image, List<ColorCluster> clusters)
    {
        using var quantizedImage = image.Clone();
        
        for (int y = 0; y < quantizedImage.Height; y++)
        {
            var row = quantizedImage.GetPixelRowSpan(y);
            for (int x = 0; x < quantizedImage.Width; x++)
            {
                var pixel = row[x];
                var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);
                
                // Find nearest cluster
                var nearestCluster = clusters
                    .OrderBy(c => ColorConverter.CalculateLabDistance(l, a, b, c.Lab_L, c.Lab_A, c.Lab_B))
                    .First();
                
                // Replace with cluster color
                row[x] = new Rgb24(nearestCluster.R, nearestCluster.G, nearestCluster.B);
            }
        }
        
        // Convert to byte array
        using var ms = new MemoryStream();
        quantizedImage.SaveAsPng(ms);
        return ms.ToArray();
    }
    
    // Helper classes
    private class LabPixel
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public double Lab_L { get; set; }
        public double Lab_A { get; set; }
        public double Lab_B { get; set; }
    }
    
    private class ColorCluster
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public double Lab_L { get; set; }
        public double Lab_A { get; set; }
        public double Lab_B { get; set; }
        public int PixelCount { get; set; }
        public List<LabPixel> AssignedPixels { get; set; } = new();
    }
}
```

### Step 25: Wire Up Quantization in Controller

Update `PatternController.cs` to call the quantization service:

```csharp
// Add to constructor
private readonly IColorQuantizationService _colorService;

public PatternController(
    StitchLensDbContext context,
    IImageProcessingService imageService,
    IColorQuantizationService colorService)
{
    _context = context;
    _imageService = imageService;
    _colorService = colorService;
}

// Update the Configure POST action
[HttpPost]
public async Task<IActionResult> Configure(ConfigureViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.YarnBrands = await _context.YarnBrands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToListAsync();
        
        return View(model);
    }
    
    var project = await _context.Projects.FindAsync(model.ProjectId);
    if (project == null)
        return NotFound();
    
    // Update project with settings
    project.MeshCount = model.MeshCount;
    project.WidthInches = model.WidthInches;
    project.HeightInches = model.HeightInches;
    project.MaxColors = model.MaxColors;
    project.StitchType = model.StitchType;
    project.YarnBrandId = model.YarnBrandId;
    
    await _context.SaveChangesAsync();
    
    // Generate quantized pattern
    var imageBytes = await System.IO.File.ReadAllBytesAsync(project.OriginalImagePath);
    var quantized = await _colorService.QuantizeAsync(imageBytes, project.MaxColors);
    
    // Save quantized image
    var quantizedFileName = $"{Path.GetFileNameWithoutExtension(project.OriginalImagePath)}_quantized.png";
    var quantizedPath = Path.Combine(Path.GetDirectoryName(project.OriginalImagePath)!, quantizedFileName);
    await System.IO.File.WriteAllBytesAsync(quantizedPath, quantized.QuantizedImageData);
    project.ProcessedImagePath = quantizedPath;
    
    // Store palette as JSON
    project.PaletteJson = System.Text.Json.JsonSerializer.Serialize(quantized.Palette);
    
    await _context.SaveChangesAsync();
    
    return RedirectToAction("Preview", new { id = project.Id });
}
```

### Step 26: Register Color Quantization Service

Update `Program.cs`:

```csharp
// Add with other services
builder.Services.AddScoped<IColorQuantizationService, ColorQuantizationService>();
```

### Step 27: Update Preview Page to Show Results

Replace `Views/Pattern/Preview.cshtml`:

```html
@using System.Text.Json
@model StitchLens.Data.Models.Project
@{
    ViewData["Title"] = "Pattern Preview";
    var palette = string.IsNullOrEmpty(Model.PaletteJson) 
        ? new List<StitchLens.Core.Services.ColorInfo>() 
        : JsonSerializer.Deserialize<List<StitchLens.Core.Services.ColorInfo>>(Model.PaletteJson) ?? new List<StitchLens.Core.Services.ColorInfo>();
}

<div class="container mt-4">
    <h2>Your Pattern Preview</h2>
    
    <div class="row">
        <div class="col-md-6">
            <div class="card">
                <div class="card-header">
                    <h5>Original Image</h5>
                </div>
                <div class="card-body text-center">
                    <img src="/uploads/@Path.GetFileName(Model.OriginalImagePath)" 
                         alt="Original" 
                         class="img-fluid"
                         style="max-height: 400px;">
                </div>
            </div>
        </div>
        
        <div class="col-md-6">
            <div class="card">
                <div class="card-header">
                    <h5>Quantized (@Model.MaxColors Colors)</h5>
                </div>
                <div class="card-body text-center">
                    @if (!string.IsNullOrEmpty(Model.ProcessedImagePath))
                    {
                        <img src="/uploads/@Path.GetFileName(Model.ProcessedImagePath)" 
                             alt="Quantized" 
                             class="img-fluid"
                             style="max-height: 400px;">
                    }
                    else
                    {
                        <p class="text-muted">Processing...</p>
                    }
                </div>
            </div>
        </div>
    </div>
    
    <div class="card mt-4">
        <div class="card-header">
            <h5>Color Palette (@palette.Count colors)</h5>
        </div>
        <div class="card-body">
            <div class="row">
                @foreach (var color in palette)
                {
                    var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    var percentage = Model.WidthInches * Model.HeightInches > 0 
                        ? (color.PixelCount / (decimal)(Model.WidthInches * Model.MeshCount * Model.HeightInches * Model.MeshCount) * 100)
                        : 0;
                    
                    <div class="col-md-3 col-sm-4 col-6 mb-3">
                        <div class="d-flex align-items-center">
                            <div style="width: 50px; height: 50px; background-color: @hexColor; border: 1px solid #ccc; margin-right: 10px;"></div>
                            <div>
                                <small class="d-block"><strong>@hexColor</strong></small>
                                <small class="text-muted">@color.PixelCount px (@percentage.ToString("F1")%)</small>
                            </div>
                        </div>
                    </div>
                }
            </div>
        </div>
    </div>
    
    <div class="card mt-4">
        <div class="card-body">
            <h5>Pattern Settings</h5>
            <ul>
                <li>Mesh Count: @Model.MeshCount</li>
                <li>Size: @Model.WidthInches.ToString("F1")" × @Model.HeightInches.ToString("F1")"</li>
                <li>Stitches: @((int)(Model.WidthInches * Model.MeshCount)) × @((int)(Model.HeightInches * Model.MeshCount))</li>
                <li>Max Colors: @Model.MaxColors</li>
                <li>Stitch Type: @Model.StitchType</li>
            </ul>
        </div>
    </div>
    
    <div class="mt-3">
        <a asp-action="Configure" asp-route-id="@Model.Id" class="btn btn-secondary">
            Adjust Settings
        </a>
        <button class="btn btn-success" disabled>
            Match Yarn Colors (Coming Next!)
        </button>
    </div>
</div>
```

### Step 28: Test Color Quantization!

```bash
dotnet watch run
```

Now:
1. Upload an image
2. Crop it
3. Configure settings (try different color counts: 20, 40, 60)
4. Click "Generate Pattern"
5. See your quantized image with the color palette!

**What you should see:**
- Side-by-side original vs quantized image
- Color palette swatches showing the exact colors used
- Pixel counts and percentages for each color

Try different images and color counts to see how the K-means algorithm adapts!


## Phase 5: Yarn Matching with ΔE2000

Now let's match those quantized colors to real yarn colors!

### Step 29: Create DMC Yarn Catalog Data

Create `StitchLens.Data/SeedData/dmc-colors.json` in your project:

```json
[
  {"code": "White", "name": "White", "hex": "#FFFFFF", "lab_l": 100.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "Ecru", "name": "Ecru", "hex": "#F0E8D8", "lab_l": 92.5, "lab_a": 0.5, "lab_b": 10.5},
  {"code": "B5200", "name": "Snow White", "hex": "#FAFAFA", "lab_l": 98.5, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "150", "name": "Dusty Rose Ultra Very Dark", "hex": "#AB4A59", "lab_l": 42.0, "lab_a": 35.0, "lab_b": 5.0},
  {"code": "151", "name": "Dusty Rose Very Light", "hex": "#F0D3D7", "lab_l": 86.0, "lab_a": 8.0, "lab_b": 2.0},
  {"code": "152", "name": "Shell Pink Medium Light", "hex": "#E0A9B0", "lab_l": 74.0, "lab_a": 18.0, "lab_b": 3.0},
  {"code": "153", "name": "Violet Very Light", "hex": "#E6D6DF", "lab_l": 87.0, "lab_a": 6.0, "lab_b": -3.0},
  {"code": "154", "name": "Grape Very Dark", "hex": "#5C2D3F", "lab_l": 25.0, "lab_a": 18.0, "lab_b": -5.0},
  {"code": "155", "name": "Blue Violet Medium Dark", "hex": "#8B9FCA", "lab_l": 66.0, "lab_a": 5.0, "lab_b": -20.0},
  {"code": "156", "name": "Blue Violet Medium Light", "hex": "#A6B8D7", "lab_l": 74.0, "lab_a": 3.0, "lab_b": -15.0},
  {"code": "157", "name": "Cornflower Blue Very Light", "hex": "#BAC5E0", "lab_l": 79.0, "lab_a": 2.0, "lab_b": -12.0},
  {"code": "158", "name": "Cornflower Blue Medium Very Dark", "hex": "#46536F", "lab_l": 37.0, "lab_a": 0.0, "lab_b": -18.0},
  {"code": "159", "name": "Blue Gray Light", "hex": "#C2CBD7", "lab_l": 82.0, "lab_a": -1.0, "lab_b": -8.0},
  {"code": "160", "name": "Blue Gray Medium", "hex": "#8FA0B5", "lab_l": 65.0, "lab_a": -2.0, "lab_b": -12.0},
  {"code": "161", "name": "Blue Gray", "hex": "#768BA0", "lab_l": 58.0, "lab_a": -3.0, "lab_b": -13.0},
  {"code": "162", "name": "Blue Ultra Very Light", "hex": "#D9E5F0", "lab_l": 91.0, "lab_a": -2.0, "lab_b": -6.0},
  {"code": "163", "name": "Celadon Green Medium", "hex": "#4F7860", "lab_l": 48.0, "lab_a": -20.0, "lab_b": 5.0},
  {"code": "164", "name": "Forest Green Light", "hex": "#C8D7AB", "lab_l": 84.0, "lab_a": -12.0, "lab_b": 20.0},
  {"code": "165", "name": "Moss Green Very Light", "hex": "#EAEBA4", "lab_l": 91.0, "lab_a": -8.0, "lab_b": 35.0},
  {"code": "166", "name": "Moss Green Medium Light", "hex": "#C4B568", "lab_l": 72.0, "lab_a": -8.0, "lab_b": 40.0},
  {"code": "167", "name": "Yellow Beige Very Dark", "hex": "#A68456", "lab_l": 57.0, "lab_a": 5.0, "lab_b": 30.0},
  {"code": "168", "name": "Pewter Very Light", "hex": "#C8C8C8", "lab_l": 81.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "169", "name": "Pewter Light", "hex": "#A8A8A8", "lab_l": 70.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "208", "name": "Lavender Very Dark", "hex": "#7B4F85", "lab_l": 42.0, "lab_a": 25.0, "lab_b": -15.0},
  {"code": "209", "name": "Lavender Dark", "hex": "#9E7FA7", "lab_l": 58.0, "lab_a": 18.0, "lab_b": -12.0},
  {"code": "210", "name": "Lavender Medium", "hex": "#C6A8CF", "lab_l": 72.0, "lab_a": 15.0, "lab_b": -10.0},
  {"code": "211", "name": "Lavender Light", "hex": "#E0CEE6", "lab_l": 85.0, "lab_a": 10.0, "lab_b": -8.0},
  {"code": "221", "name": "Shell Pink Very Dark", "hex": "#8B4651", "lab_l": 40.0, "lab_a": 28.0, "lab_b": 5.0},
  {"code": "223", "name": "Shell Pink Light", "hex": "#D39B9E", "lab_l": 70.0, "lab_a": 20.0, "lab_b": 5.0},
  {"code": "224", "name": "Shell Pink Very Light", "hex": "#E8C5C8", "lab_l": 82.0, "lab_a": 12.0, "lab_b": 3.0},
  {"code": "225", "name": "Shell Pink Ultra Very Light", "hex": "#FFE6E8", "lab_l": 93.0, "lab_a": 6.0, "lab_b": 2.0},
  {"code": "300", "name": "Mahogany Very Dark", "hex": "#6B3926", "lab_l": 28.0, "lab_a": 18.0, "lab_b": 15.0},
  {"code": "301", "name": "Mahogany Medium", "hex": "#9B5430", "lab_l": 45.0, "lab_a": 25.0, "lab_b": 25.0},
  {"code": "304", "name": "Christmas Red Medium", "hex": "#B8244D", "lab_l": 42.0, "lab_a": 60.0, "lab_b": 15.0},
  {"code": "307", "name": "Lemon", "hex": "#FFD800", "lab_l": 85.0, "lab_a": -5.0, "lab_b": 85.0},
  {"code": "309", "name": "Rose Dark", "hex": "#B8466B", "lab_l": 45.0, "lab_a": 48.0, "lab_b": 5.0},
  {"code": "310", "name": "Black", "hex": "#000000", "lab_l": 0.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "311", "name": "Navy Blue Medium", "hex": "#2E5A7A", "lab_l": 38.0, "lab_a": -8.0, "lab_b": -20.0},
  {"code": "312", "name": "Navy Blue Light", "hex": "#5279A0", "lab_l": 50.0, "lab_a": -5.0, "lab_b": -25.0},
  {"code": "315", "name": "Antique Mauve Very Dark", "hex": "#6B4555", "lab_l": 35.0, "lab_a": 20.0, "lab_b": 0.0},
  {"code": "316", "name": "Antique Mauve Medium", "hex": "#A46F7C", "lab_l": 52.0, "lab_a": 22.0, "lab_b": 0.0},
  {"code": "317", "name": "Pewter Gray", "hex": "#6C6C6C", "lab_l": 47.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "318", "name": "Steel Gray Light", "hex": "#A9A9A9", "lab_l": 71.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "319", "name": "Pistachio Green Very Dark", "hex": "#2E5947", "lab_l": 35.0, "lab_a": -20.0, "lab_b": 5.0},
  {"code": "320", "name": "Pistachio Green Medium", "hex": "#628F70", "lab_l": 55.0, "lab_a": -25.0, "lab_b": 10.0},
  {"code": "321", "name": "Christmas Red", "hex": "#C8244D", "lab_l": 45.0, "lab_a": 65.0, "lab_b": 15.0},
  {"code": "322", "name": "Navy Blue Very Light", "hex": "#7FA0C8", "lab_l": 65.0, "lab_a": 0.0, "lab_b": -22.0},
  {"code": "326", "name": "Rose Very Deep", "hex": "#A03854", "lab_l": 38.0, "lab_a": 50.0, "lab_b": 5.0},
  {"code": "327", "name": "Violet Very Dark", "hex": "#5A2F5E", "lab_l": 28.0, "lab_a": 28.0, "lab_b": -20.0},
  {"code": "333", "name": "Blue Violet Very Dark", "hex": "#5A4F7A", "lab_l": 38.0, "lab_a": 12.0, "lab_b": -25.0},
  {"code": "334", "name": "Baby Blue Medium", "hex": "#5E94C3", "lab_l": 60.0, "lab_a": -5.0, "lab_b": -28.0},
  {"code": "335", "name": "Rose", "hex": "#D8325C", "lab_l": 50.0, "lab_a": 62.0, "lab_b": 10.0},
  {"code": "336", "name": "Navy Blue", "hex": "#28456B", "lab_l": 30.0, "lab_a": -5.0, "lab_b": -22.0},
  {"code": "340", "name": "Blue Violet Medium", "hex": "#AD9FCF", "lab_l": 68.0, "lab_a": 15.0, "lab_b": -22.0},
  {"code": "341", "name": "Blue Violet Light", "hex": "#C8BFE6", "lab_l": 78.0, "lab_a": 10.0, "lab_b": -18.0},
  {"code": "347", "name": "Salmon Very Dark", "hex": "#B8243C", "lab_l": 42.0, "lab_a": 58.0, "lab_b": 25.0},
  {"code": "349", "name": "Coral Dark", "hex": "#C8324C", "lab_l": 48.0, "lab_a": 60.0, "lab_b": 20.0},
  {"code": "350", "name": "Coral Medium", "hex": "#D8465C", "lab_l": 55.0, "lab_a": 58.0, "lab_b": 18.0},
  {"code": "351", "name": "Coral", "hex": "#E86B7C", "lab_l": 62.0, "lab_a": 50.0, "lab_b": 15.0},
  {"code": "352", "name": "Coral Light", "hex": "#F88594", "lab_l": 70.0, "lab_a": 45.0, "lab_b": 12.0},
  {"code": "353", "name": "Peach", "hex": "#FDBCA4", "lab_l": 80.0, "lab_a": 20.0, "lab_b": 22.0},
  {"code": "400", "name": "Mahogany Dark", "hex": "#8B4524", "lab_l": 38.0, "lab_a": 25.0, "lab_b": 28.0},
  {"code": "402", "name": "Mahogany Very Light", "hex": "#F09F74", "lab_l": 72.0, "lab_a": 25.0, "lab_b": 35.0},
  {"code": "413", "name": "Pewter Gray Dark", "hex": "#555555", "lab_l": 38.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "414", "name": "Steel Gray Dark", "hex": "#8B8B8B", "lab_l": 60.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "415", "name": "Pearl Gray", "hex": "#D8D8D8", "lab_l": 87.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "420", "name": "Hazelnut Brown Dark", "hex": "#9B6B3C", "lab_l": 50.0, "lab_a": 12.0, "lab_b": 32.0},
  {"code": "422", "name": "Hazelnut Brown Light", "hex": "#C89F74", "lab_l": 68.0, "lab_a": 10.0, "lab_b": 28.0},
  {"code": "433", "name": "Brown Medium", "hex": "#7B4524", "lab_l": 35.0, "lab_a": 18.0, "lab_b": 25.0},
  {"code": "434", "name": "Brown Light", "hex": "#996B3C", "lab_l": 50.0, "lab_a": 15.0, "lab_b": 30.0},
  {"code": "435", "name": "Brown Very Light", "hex": "#B88F64", "lab_l": 62.0, "lab_a": 12.0, "lab_b": 28.0},
  {"code": "436", "name": "Tan", "hex": "#D3A574", "lab_l": 70.0, "lab_a": 10.0, "lab_b": 30.0},
  {"code": "437", "name": "Tan Light", "hex": "#E8C594", "lab_l": 80.0, "lab_a": 8.0, "lab_b": 28.0},
  {"code": "444", "name": "Lemon Dark", "hex": "#FFD700", "lab_l": 85.0, "lab_a": -8.0, "lab_b": 82.0},
  {"code": "445", "name": "Lemon Light", "hex": "#FFED99", "lab_l": 93.0, "lab_a": -5.0, "lab_b": 45.0},
  {"code": "469", "name": "Avocado Green", "hex": "#6B7A3C", "lab_l": 48.0, "lab_a": -12.0, "lab_b": 28.0},
  {"code": "470", "name": "Avocado Green Light", "hex": "#8FA054", "lab_l": 62.0, "lab_a": -15.0, "lab_b": 35.0},
  {"code": "471", "name": "Avocado Green Very Light", "hex": "#B5C574", "lab_l": 75.0, "lab_a": -18.0, "lab_b": 40.0},
  {"code": "472", "name": "Avocado Green Ultra Light", "hex": "#D8ED99", "lab_l": 90.0, "lab_a": -15.0, "lab_b": 45.0},
  {"code": "498", "name": "Christmas Red Dark", "hex": "#A02848", "lab_l": 38.0, "lab_a": 55.0, "lab_b": 10.0},
  {"code": "500", "name": "Blue Green Very Dark", "hex": "#0F4F41", "lab_l": 30.0, "lab_a": -22.0, "lab_b": 0.0},
  {"code": "501", "name": "Blue Green Dark", "hex": "#3D6F5E", "lab_l": 42.0, "lab_a": -25.0, "lab_b": 2.0},
  {"code": "502", "name": "Blue Green", "hex": "#5E8F7C", "lab_l": 55.0, "lab_a": -25.0, "lab_b": 5.0},
  {"code": "503", "name": "Blue Green Medium", "hex": "#86AF9A", "lab_l": 68.0, "lab_a": -22.0, "lab_b": 8.0},
  {"code": "504", "name": "Blue Green Very Light", "hex": "#B5D8C8", "lab_l": 82.0, "lab_a": -18.0, "lab_b": 8.0},
  {"code": "517", "name": "Wedgwood Dark", "hex": "#3C5F7A", "lab_l": 40.0, "lab_a": -8.0, "lab_b": -18.0},
  {"code": "518", "name": "Wedgwood Light", "hex": "#5E8FAF", "lab_l": 58.0, "lab_a": -8.0, "lab_b": -22.0},
  {"code": "519", "name": "Sky Blue", "hex": "#8FC5E0", "lab_l": 78.0, "lab_a": -12.0, "lab_b": -18.0},
  {"code": "520", "name": "Fern Green Dark", "hex": "#5A6F47", "lab_l": 45.0, "lab_a": -15.0, "lab_b": 15.0},
  {"code": "522", "name": "Fern Green", "hex": "#8FAF86", "lab_l": 68.0, "lab_a": -18.0, "lab_b": 18.0},
  {"code": "523", "name": "Fern Green Light", "hex": "#B5D3AB", "lab_l": 82.0, "lab_a": -15.0, "lab_b": 18.0},
  {"code": "524", "name": "Fern Green Very Light", "hex": "#D0E8C8", "lab_l": 90.0, "lab_a": -12.0, "lab_b": 15.0},
  {"code": "535", "name": "Ash Gray Very Light", "hex": "#555555", "lab_l": 38.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "543", "name": "Beige Brown Ultra Very Light", "hex": "#F0D8BC", "lab_l": 87.0, "lab_a": 5.0, "lab_b": 18.0},
  {"code": "550", "name": "Violet Very Dark", "hex": "#4F2F5A", "lab_l": 25.0, "lab_a": 25.0, "lab_b": -22.0},
  {"code": "552", "name": "Violet Medium", "hex": "#7A5F85", "lab_l": 45.0, "lab_a": 20.0, "lab_b": -18.0},
  {"code": "553", "name": "Violet", "hex": "#9F7FAA", "lab_l": 58.0, "lab_a": 22.0, "lab_b": -15.0},
  {"code": "554", "name": "Violet Light", "hex": "#C4A8CF", "lab_l": 72.0, "lab_a": 18.0, "lab_b": -12.0},
  {"code": "561", "name": "Jade Very Dark", "hex": "#2F5947", "lab_l": 35.0, "lab_a": -22.0, "lab_b": 5.0},
  {"code": "562", "name": "Jade Medium", "hex": "#5A8F70", "lab_l": 55.0, "lab_a": -28.0, "lab_b": 10.0},
  {"code": "563", "name": "Jade Light", "hex": "#86BFA0", "lab_l": 72.0, "lab_a": -28.0, "lab_b": 12.0},
  {"code": "564", "name": "Jade Very Light", "hex": "#B5D8C8", "lab_l": 85.0, "lab_a": -22.0, "lab_b": 10.0},
  {"code": "580", "name": "Moss Green Dark", "hex": "#6B7A3C", "lab_l": 48.0, "lab_a": -12.0, "lab_b": 28.0},
  {"code": "581", "name": "Moss Green", "hex": "#8FAF54", "lab_l": 68.0, "lab_a": -18.0, "lab_b": 38.0},
  {"code": "597", "name": "Turquoise", "hex": "#5EBFC8", "lab_l": 72.0, "lab_a": -28.0, "lab_b": -8.0},
  {"code": "598", "name": "Turquoise Light", "hex": "#86D8E0", "lab_l": 82.0, "lab_a": -25.0, "lab_b": -10.0},
  {"code": "600", "name": "Cranberry Very Dark", "hex": "#D82854", "lab_l": 48.0, "lab_a": 65.0, "lab_b": 10.0},
  {"code": "601", "name": "Cranberry Dark", "hex": "#E8466B", "lab_l": 55.0, "lab_a": 60.0, "lab_b": 8.0},
  {"code": "602", "name": "Cranberry Medium", "hex": "#F8647C", "lab_l": 62.0, "lab_a": 58.0, "lab_b": 8.0},
  {"code": "603", "name": "Cranberry", "hex": "#FF859A", "lab_l": 70.0, "lab_a": 48.0, "lab_b": 8.0},
  {"code": "604", "name": "Cranberry Light", "hex": "#FFA8B4", "lab_l": 78.0, "lab_a": 35.0, "lab_b": 8.0},
  {"code": "605", "name": "Cranberry Very Light", "hex": "#FFCED6", "lab_l": 87.0, "lab_a": 18.0, "lab_b": 5.0},
  {"code": "606", "name": "Bright Orange-Red", "hex": "#FA3C28", "lab_l": 52.0, "lab_a": 70.0, "lab_b": 55.0},
  {"code": "608", "name": "Bright Orange", "hex": "#FD5F28", "lab_l": 58.0, "lab_a": 60.0, "lab_b": 60.0},
  {"code": "610", "name": "Drab Brown Very Dark", "hex": "#6B5437", "lab_l": 38.0, "lab_a": 5.0, "lab_b": 20.0},
  {"code": "611", "name": "Drab Brown Dark", "hex": "#8F6F4F", "lab_l": 50.0, "lab_a": 5.0, "lab_b": 22.0},
  {"code": "612", "name": "Drab Brown Medium", "hex": "#AF8F6B", "lab_l": 62.0, "lab_a": 5.0, "lab_b": 24.0},
  {"code": "613", "name": "Drab Brown Light", "hex": "#D3B593", "lab_l": 75.0, "lab_a": 5.0, "lab_b": 25.0},
  {"code": "632", "name": "Desert Sand Ultra Very Dark", "hex": "#8B5437", "lab_l": 42.0, "lab_a": 18.0, "lab_b": 25.0},
  {"code": "640", "name": "Beige Gray Very Dark", "hex": "#8F7A64", "lab_l": 52.0, "lab_a": 5.0, "lab_b": 15.0},
  {"code": "642", "name": "Beige Gray Dark", "hex": "#AF9A7C", "lab_l": 65.0, "lab_a": 5.0, "lab_b": 18.0},
  {"code": "644", "name": "Beige Gray Medium", "hex": "#D3C5AB", "lab_l": 80.0, "lab_a": 3.0, "lab_b": 15.0},
  {"code": "645", "name": "Beaver Gray Very Dark", "hex": "#6B6B6B", "lab_l": 47.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "646", "name": "Beaver Gray Dark", "hex": "#8B8B8B", "lab_l": 60.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "647", "name": "Beaver Gray Medium", "hex": "#AFAFAF", "lab_l": 72.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "648", "name": "Beaver Gray Light", "hex": "#C8C8C8", "lab_l": 82.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "666", "name": "Bright Red", "hex": "#E8243C", "lab_l": 50.0, "lab_a": 70.0, "lab_b": 35.0},
  {"code": "676", "name": "Old Gold Light", "hex": "#E8C594", "lab_l": 80.0, "lab_a": 5.0, "lab_b": 35.0},
  {"code": "677", "name": "Old Gold Very Light", "hex": "#F0DDB4", "lab_l": 88.0, "lab_a": 3.0, "lab_b": 28.0},
  {"code": "680", "name": "Old Gold Dark", "hex": "#C89F54", "lab_l": 68.0, "lab_a": 8.0, "lab_b": 45.0},
  {"code": "699", "name": "Christmas Green", "hex": "#0F5F3C", "lab_l": 35.0, "lab_a": -35.0, "lab_b": 10.0},
  {"code": "700", "name": "Christmas Green Bright", "hex": "#1F6F4C", "lab_l": 42.0, "lab_a": -38.0, "lab_b": 12.0},
  {"code": "701", "name": "Christmas Green Light", "hex": "#4F8F64", "lab_l": 55.0, "lab_a": -35.0, "lab_b": 15.0},
  {"code": "702", "name": "Kelly Green", "hex": "#5FA074", "lab_l": 62.0, "lab_a": -32.0, "lab_b": 15.0},
  {"code": "703", "name": "Chartreuse", "hex": "#7ABF8C", "lab_l": 72.0, "lab_a": -30.0, "lab_b": 20.0},
  {"code": "704", "name": "Chartreuse Bright", "hex": "#9FD89A", "lab_l": 82.0, "lab_a": -28.0, "lab_b": 22.0},
  {"code": "712", "name": "Cream", "hex": "#FFFFF0", "lab_l": 98.0, "lab_a": 0.0, "lab_b": 8.0},
  {"code": "718", "name": "Plum", "hex": "#A03864", "lab_l": 42.0, "lab_a": 48.0, "lab_b": -5.0},
  {"code": "720", "name": "Orange Spice Dark", "hex": "#E85428", "lab_l": 55.0, "lab_a": 52.0, "lab_b": 55.0},
  {"code": "721", "name": "Orange Spice Medium", "hex": "#F87438", "lab_l": 62.0, "lab_a": 48.0, "lab_b": 58.0},
  {"code": "722", "name": "Orange Spice Light", "hex": "#FF9854", "lab_l": 72.0, "lab_a": 35.0, "lab_b": 52.0},
  {"code": "725", "name": "Topaz", "hex": "#FFCE54", "lab_l": 82.0, "lab_a": 8.0, "lab_b": 65.0},
  {"code": "726", "name": "Topaz Light", "hex": "#FFD86B", "lab_l": 87.0, "lab_a": 5.0, "lab_b": 68.0},
  {"code": "727", "name": "Topaz Very Light", "hex": "#FFED99", "lab_l": 93.0, "lab_a": 0.0, "lab_b": 48.0},
  {"code": "729", "name": "Old Gold Medium", "hex": "#D3A564", "lab_l": 70.0, "lab_a": 8.0, "lab_b": 42.0},
  {"code": "730", "name": "Olive Green Very Dark", "hex": "#8B7A3C", "lab_l": 50.0, "lab_a": -5.0, "lab_b": 35.0},
  {"code": "731", "name": "Olive Green Dark", "hex": "#AF9854", "lab_l": 62.0, "lab_a": -5.0, "lab_b": 38.0},
  {"code": "732", "name": "Olive Green", "hex": "#B8A86B", "lab_l": 68.0, "lab_a": -5.0, "lab_b": 35.0},
  {"code": "733", "name": "Olive Green Medium", "hex": "#C8B57C", "lab_l": 73.0, "lab_a": -3.0, "lab_b": 32.0},
  {"code": "734", "name": "Olive Green Light", "hex": "#D8CE94", "lab_l": 82.0, "lab_a": -5.0, "lab_b": 35.0},
  {"code": "738", "name": "Tan Very Light", "hex": "#E8C8A4", "lab_l": 82.0, "lab_a": 8.0, "lab_b": 22.0},
  {"code": "739", "name": "Tan Ultra Very Light", "hex": "#F0DEC8", "lab_l": 90.0, "lab_a": 5.0, "lab_b": 18.0},
  {"code": "740", "name": "Tangerine", "hex": "#FF8C28", "lab_l": 68.0, "lab_a": 38.0, "lab_b": 68.0},
  {"code": "741", "name": "Tangerine Medium", "hex": "#FFA038", "lab_l": 72.0, "lab_a": 28.0, "lab_b": 65.0},
  {"code": "742", "name": "Tangerine Light", "hex": "#FFB854", "lab_l": 78.0, "lab_a": 18.0, "lab_b": 60.0},
  {"code": "743", "name": "Yellow Medium", "hex": "#FFCE6B", "lab_l": 84.0, "lab_a": 8.0, "lab_b": 62.0},
  {"code": "744", "name": "Yellow Pale", "hex": "#FFE57C", "lab_l": 91.0, "lab_a": 0.0, "lab_b": 52.0},
  {"code": "745", "name": "Yellow Light Pale", "hex": "#FFF0AB", "lab_l": 95.0, "lab_a": -3.0, "lab_b": 38.0},
  {"code": "746", "name": "Off White", "hex": "#F8F8E8", "lab_l": 97.0, "lab_a": 0.0, "lab_b": 8.0},
  {"code": "747", "name": "Sky Blue Very Light", "hex": "#E0F0FF", "lab_l": 94.0, "lab_a": -5.0, "lab_b": -8.0},
  {"code": "754", "name": "Peach Light", "hex": "#FFD8BC", "lab_l": 88.0, "lab_a": 12.0, "lab_b": 22.0},
  {"code": "758", "name": "Terra Cotta Very Light", "hex": "#E8B8A4", "lab_l": 78.0, "lab_a": 15.0, "lab_b": 18.0},
  {"code": "760", "name": "Salmon", "hex": "#FFA8BC", "lab_l": 78.0, "lab_a": 32.0, "lab_b": 8.0},
  {"code": "761", "name": "Salmon Light", "hex": "#FFCED6", "lab_l": 87.0, "lab_a": 18.0, "lab_b": 5.0},
  {"code": "762", "name": "Pearl Gray Very Light", "hex": "#E8E8E8", "lab_l": 92.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "772", "name": "Yellow Green Very Light", "hex": "#E0F0C8", "lab_l": 92.0, "lab_a": -12.0, "lab_b": 25.0},
  {"code": "775", "name": "Baby Blue Very Light", "hex": "#D0E8F0", "lab_l": 90.0, "lab_a": -8.0, "lab_b": -8.0},
  {"code": "776", "name": "Pink Medium", "hex": "#FFBCD6", "lab_l": 82.0, "lab_a": 25.0, "lab_b": -2.0},
  {"code": "778", "name": "Antique Mauve Very Light", "hex": "#E8C8D6", "lab_l": 85.0, "lab_a": 12.0, "lab_b": -3.0},
  {"code": "780", "name": "Topaz Ultra Very Dark", "hex": "#996B28", "lab_l": 48.0, "lab_a": 15.0, "lab_b": 42.0},
  {"code": "781", "name": "Topaz Very Dark", "hex": "#AF7C38", "lab_l": 55.0, "lab_a": 15.0, "lab_b": 45.0},
  {"code": "782", "name": "Topaz Dark", "hex": "#C8944C", "lab_l": 64.0, "lab_a": 12.0, "lab_b": 48.0},
  {"code": "783", "name": "Topaz Medium", "hex": "#D8A85C", "lab_l": 72.0, "lab_a": 10.0, "lab_b": 52.0},
  {"code": "791", "name": "Cornflower Blue Very Dark", "hex": "#3C4F7A", "lab_l": 35.0, "lab_a": 5.0, "lab_b": -28.0},
  {"code": "792", "name": "Cornflower Blue Dark", "hex": "#5A6F9F", "lab_l": 48.0, "lab_a": 5.0, "lab_b": -30.0},
  {"code": "793", "name": "Cornflower Blue Medium", "hex": "#7A8FBF", "lab_l": 60.0, "lab_a": 5.0, "lab_b": -28.0},
  {"code": "794", "name": "Cornflower Blue Light", "hex": "#9FB4D8", "lab_l": 73.0, "lab_a": 3.0, "lab_b": -22.0},
  {"code": "796", "name": "Royal Blue Dark", "hex": "#1F3C6F", "lab_l": 28.0, "lab_a": 5.0, "lab_b": -32.0},
  {"code": "797", "name": "Royal Blue", "hex": "#2F4F8F", "lab_l": 35.0, "lab_a": 5.0, "lab_b": -35.0},
  {"code": "798", "name": "Delft Blue Dark", "hex": "#3F5FAF", "lab_l": 42.0, "lab_a": 5.0, "lab_b": -38.0},
  {"code": "799", "name": "Delft Blue Medium", "hex": "#5F7FCF", "lab_l": 55.0, "lab_a": 5.0, "lab_b": -40.0},
  {"code": "800", "name": "Delft Blue Pale", "hex": "#C0D8F0", "lab_l": 85.0, "lab_a": 0.0, "lab_b": -15.0},
  {"code": "801", "name": "Coffee Brown Dark", "hex": "#5C3926", "lab_l": 28.0, "lab_a": 15.0, "lab_b": 18.0},
  {"code": "803", "name": "Baby Blue Ultra Very Dark", "hex": "#2F4F7A", "lab_l": 35.0, "lab_a": 0.0, "lab_b": -25.0},
  {"code": "806", "name": "Peacock Blue Dark", "hex": "#3F7F9F", "lab_l": 50.0, "lab_a": -15.0, "lab_b": -18.0},
  {"code": "807", "name": "Peacock Blue", "hex": "#5F9FBF", "lab_l": 62.0, "lab_a": -15.0, "lab_b": -20.0},
  {"code": "809", "name": "Delft Blue", "hex": "#8FB4CF", "lab_l": 72.0, "lab_a": -5.0, "lab_b": -18.0},
  {"code": "813", "name": "Blue Light", "hex": "#9FC5D8", "lab_l": 78.0, "lab_a": -8.0, "lab_b": -12.0},
  {"code": "814", "name": "Garnet Dark", "hex": "#7A1838", "lab_l": 28.0, "lab_a": 52.0, "lab_b": 5.0},
  {"code": "815", "name": "Garnet Medium", "hex": "#932848", "lab_l": 35.0, "lab_a": 50.0, "lab_b": 5.0},
  {"code": "816", "name": "Garnet", "hex": "#AB3858", "lab_l": 42.0, "lab_a": 48.0, "lab_b": 5.0},
  {"code": "817", "name": "Coral Red Very Dark", "hex": "#B82838", "lab_l": 45.0, "lab_a": 62.0, "lab_b": 28.0},
  {"code": "818", "name": "Baby Pink", "hex": "#FFE6ED", "lab_l": 93.0, "lab_a": 8.0, "lab_b": 0.0},
  {"code": "819", "name": "Baby Pink Light", "hex": "#FFF0F5", "lab_l": 96.0, "lab_a": 5.0, "lab_b": 0.0},
  {"code": "820", "name": "Royal Blue Very Dark", "hex": "#0F2858", "lab_l": 20.0, "lab_a": 8.0, "lab_b": -28.0},
  {"code": "822", "name": "Beige Gray Light", "hex": "#E8DCC8", "lab_l": 88.0, "lab_a": 3.0, "lab_b": 12.0},
  {"code": "823", "name": "Navy Blue Dark", "hex": "#1F3858", "lab_l": 25.0, "lab_a": 0.0, "lab_b": -20.0},
  {"code": "824", "name": "Blue Very Dark", "hex": "#2F4F7A", "lab_l": 35.0, "lab_a": 0.0, "lab_b": -25.0},
  {"code": "825", "name": "Blue Dark", "hex": "#3F5F8F", "lab_l": 42.0, "lab_a": 0.0, "lab_b": -28.0},
  {"code": "826", "name": "Blue Medium", "hex": "#5F7FAF", "lab_l": 52.0, "lab_a": 0.0, "lab_b": -30.0},
  {"code": "827", "name": "Blue Very Light", "hex": "#BFD8F0", "lab_l": 85.0, "lab_a": -3.0, "lab_b": -12.0},
  {"code": "828", "name": "Blue Ultra Very Light", "hex": "#D8EDF8", "lab_l": 92.0, "lab_a": -5.0, "lab_b": -8.0},
  {"code": "829", "name": "Golden Olive Very Dark", "hex": "#8B7328", "lab_l": 48.0, "lab_a": 0.0, "lab_b": 38.0},
  {"code": "830", "name": "Golden Olive Dark", "hex": "#9F8638", "lab_l": 55.0, "lab_a": 0.0, "lab_b": 40.0},
  {"code": "831", "name": "Golden Olive Medium", "hex": "#B39F48", "lab_l": 65.0, "lab_a": 0.0, "lab_b": 42.0},
  {"code": "832", "name": "Golden Olive", "hex": "#C8B35C", "lab_l": 72.0, "lab_a": 0.0, "lab_b": 45.0},
  {"code": "833", "name": "Golden Olive Light", "hex": "#D8C874", "lab_l": 80.0, "lab_a": -3.0, "lab_b": 45.0},
  {"code": "834", "name": "Golden Olive Very Light", "hex": "#E8D894", "lab_l": 87.0, "lab_a": -5.0, "lab_b": 42.0},
  {"code": "838", "name": "Beige Brown Very Dark", "hex": "#5C4528", "lab_l": 32.0, "lab_a": 8.0, "lab_b": 22.0},
  {"code": "839", "name": "Beige Brown Dark", "hex": "#6F5638", "lab_l": 40.0, "lab_a": 8.0, "lab_b": 24.0},
  {"code": "840", "name": "Beige Brown Medium", "hex": "#8F7348", "lab_l": 50.0, "lab_a": 8.0, "lab_b": 28.0},
  {"code": "841", "name": "Beige Brown Light", "hex": "#AF8F64", "lab_l": 62.0, "lab_a": 8.0, "lab_b": 30.0},
  {"code": "842", "name": "Beige Brown Very Light", "hex": "#D3B593", "lab_l": 75.0, "lab_a": 8.0, "lab_b": 28.0},
  {"code": "844", "name": "Beaver Gray Ultra Dark", "hex": "#484848", "lab_l": 32.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "869", "name": "Hazelnut Brown Very Dark", "hex": "#8B5428", "lab_l": 42.0, "lab_a": 18.0, "lab_b": 32.0},
  {"code": "890", "name": "Pistachio Green Ultra Dark", "hex": "#1F4528", "lab_l": 28.0, "lab_a": -22.0, "lab_b": 8.0},
  {"code": "891", "name": "Carnation Dark", "hex": "#FF3864", "lab_l": 55.0, "lab_a": 68.0, "lab_b": 5.0},
  {"code": "892", "name": "Carnation Medium", "hex": "#FF5C7C", "lab_l": 62.0, "lab_a": 60.0, "lab_b": 5.0},
  {"code": "893", "name": "Carnation Light", "hex": "#FF8094", "lab_l": 70.0, "lab_a": 50.0, "lab_b": 5.0},
  {"code": "894", "name": "Carnation Very Light", "hex": "#FFAABE", "lab_l": 78.0, "lab_a": 35.0, "lab_b": 3.0},
  {"code": "895", "name": "Hunter Green Very Dark", "hex": "#1F4528", "lab_l": 28.0, "lab_a": -25.0, "lab_b": 8.0},
  {"code": "898", "name": "Coffee Brown Very Dark", "hex": "#493026", "lab_l": 24.0, "lab_a": 12.0, "lab_b": 15.0},
  {"code": "899", "name": "Rose Medium", "hex": "#E85C78", "lab_l": 58.0, "lab_a": 55.0, "lab_b": 8.0},
  {"code": "900", "name": "Burnt Orange Dark", "hex": "#D84028", "lab_l": 48.0, "lab_a": 58.0, "lab_b": 48.0},
  {"code": "902", "name": "Garnet Very Dark", "hex": "#8B1838", "lab_l": 32.0, "lab_a": 55.0, "lab_b": 5.0},
  {"code": "904", "name": "Parrot Green Very Dark", "hex": "#5F7A28", "lab_l": 48.0, "lab_a": -22.0, "lab_b": 32.0},
  {"code": "905", "name": "Parrot Green Dark", "hex": "#6F8F38", "lab_l": 55.0, "lab_a": -25.0, "lab_b": 35.0},
  {"code": "906", "name": "Parrot Green Medium", "hex": "#8FAF48", "lab_l": 68.0, "lab_a": -28.0, "lab_b": 42.0},
  {"code": "907", "name": "Parrot Green Light", "hex": "#B8CE64", "lab_l": 80.0, "lab_a": -25.0, "lab_b": 45.0},
  {"code": "909", "name": "Emerald Green Very Dark", "hex": "#1F6B38", "lab_l": 40.0, "lab_a": -38.0, "lab_b": 15.0},
  {"code": "910", "name": "Emerald Green Dark", "hex": "#2F7F48", "lab_l": 48.0, "lab_a": -40.0, "lab_b": 18.0},
  {"code": "911", "name": "Emerald Green Medium", "hex": "#3F9F58", "lab_l": 60.0, "lab_a": -42.0, "lab_b": 20.0},
  {"code": "912", "name": "Emerald Green Light", "hex": "#5FAF74", "lab_l": 68.0, "lab_a": -38.0, "lab_b": 20.0},
  {"code": "913", "name": "Nile Green Medium", "hex": "#8FC8A0", "lab_l": 78.0, "lab_a": -28.0, "lab_b": 15.0},
  {"code": "915", "name": "Plum Dark", "hex": "#932854", "lab_l": 38.0, "lab_a": 50.0, "lab_b": -8.0},
  {"code": "917", "name": "Plum Medium", "hex": "#A83864", "lab_l": 45.0, "lab_a": 48.0, "lab_b": -8.0},
  {"code": "918", "name": "Red Copper Dark", "hex": "#8B3828", "lab_l": 38.0, "lab_a": 42.0, "lab_b": 28.0},
  {"code": "919", "name": "Red Copper", "hex": "#A04838", "lab_l": 45.0, "lab_a": 40.0, "lab_b": 28.0},
  {"code": "920", "name": "Copper Medium", "hex": "#B85C48", "lab_l": 52.0, "lab_a": 35.0, "lab_b": 28.0},
  {"code": "921", "name": "Copper", "hex": "#C87458", "lab_l": 58.0, "lab_a": 30.0, "lab_b": 28.0},
  {"code": "922", "name": "Copper Light", "hex": "#E89474", "lab_l": 68.0, "lab_a": 25.0, "lab_b": 28.0},
  {"code": "924", "name": "Gray Green Very Dark", "hex": "#5A6F64", "lab_l": 45.0, "lab_a": -12.0, "lab_b": 5.0},
  {"code": "926", "name": "Gray Green Medium", "hex": "#7A9F8C", "lab_l": 62.0, "lab_a": -18.0, "lab_b": 8.0},
  {"code": "927", "name": "Gray Green Light", "hex": "#A0BFB4", "lab_l": 75.0, "lab_a": -15.0, "lab_b": 8.0},
  {"code": "928", "name": "Gray Green Very Light", "hex": "#C8D8D0", "lab_l": 85.0, "lab_a": -8.0, "lab_b": 5.0},
  {"code": "930", "name": "Antique Blue Dark", "hex": "#3C5F6F", "lab_l": 40.0, "lab_a": -10.0, "lab_b": -10.0},
  {"code": "931", "name": "Antique Blue Medium", "hex": "#5A7F8F", "lab_l": 52.0, "lab_a": -12.0, "lab_b": -12.0},
  {"code": "932", "name": "Antique Blue Light", "hex": "#8FAFBF", "lab_l": 70.0, "lab_a": -12.0, "lab_b": -12.0},
  {"code": "934", "name": "Black Avocado Green", "hex": "#3C4528", "lab_l": 28.0, "lab_a": -8.0, "lab_b": 15.0},
  {"code": "935", "name": "Avocado Green Dark", "hex": "#4F5A38", "lab_l": 38.0, "lab_a": -10.0, "lab_b": 18.0},
  {"code": "936", "name": "Avocado Green Very Dark", "hex": "#3F4F28", "lab_l": 32.0, "lab_a": -12.0, "lab_b": 20.0},
  {"code": "937", "name": "Avocado Green Medium", "hex": "#6F7A48", "lab_l": 48.0, "lab_a": -12.0, "lab_b": 25.0},
  {"code": "938", "name": "Coffee Brown Ultra Dark", "hex": "#381F18", "lab_l": 15.0, "lab_a": 10.0, "lab_b": 12.0},
  {"code": "939", "name": "Navy Blue Very Dark", "lab_l": 18.0, "lab_a": 5.0, "lab_b": -22.0},
  {"code": "943", "name": "Aquamarine Medium", "hex": "#3FAF8C", "lab_l": 65.0, "lab_a": -35.0, "lab_b": 8.0},
  {"code": "945", "name": "Tawny", "hex": "#FFBCA4", "lab_l": 80.0, "lab_a": 18.0, "lab_b": 22.0},
  {"code": "946", "name": "Burnt Orange Medium", "hex": "#E85428", "lab_l": 55.0, "lab_a": 55.0, "lab_b": 52.0},
  {"code": "947", "name": "Burnt Orange", "hex": "#FF6C38", "lab_l": 62.0, "lab_a": 48.0, "lab_b": 52.0},
  {"code": "948", "name": "Peach Very Light", "hex": "#FFE8D8", "lab_l": 92.0, "lab_a": 8.0, "lab_b": 15.0},
  {"code": "950", "name": "Desert Sand Light", "hex": "#E8C8B4", "lab_l": 82.0, "lab_a": 10.0, "lab_b": 15.0},
  {"code": "951", "name": "Tawny Light", "hex": "#FFD8C8", "lab_l": 88.0, "lab_a": 12.0, "lab_b": 15.0},
  {"code": "954", "name": "Nile Green", "hex": "#A0D8B4", "lab_l": 82.0, "lab_a": -28.0, "lab_b": 12.0},
  {"code": "955", "name": "Nile Green Light", "hex": "#BFE8C8", "lab_l": 90.0, "lab_a": -22.0, "lab_b": 12.0},
  {"code": "956", "name": "Geranium", "hex": "#FF5C78", "lab_l": 60.0, "lab_a": 62.0, "lab_b": 8.0},
  {"code": "957", "name": "Geranium Pale", "hex": "#FF94A8", "lab_l": 72.0, "lab_a": 45.0, "lab_b": 5.0},
  {"code": "958", "name": "Sea Green Dark", "hex": "#3F9F8C", "lab_l": 60.0, "lab_a": -32.0, "lab_b": 5.0},
  {"code": "959", "name": "Sea Green Medium", "hex": "#5FBFA0", "lab_l": 72.0, "lab_a": -35.0, "lab_b": 8.0},
  {"code": "961", "name": "Dusty Rose Dark", "hex": "#D85C7C", "lab_l": 58.0, "lab_a": 48.0, "lab_b": 5.0},
  {"code": "962", "name": "Dusty Rose Medium", "hex": "#E8749A", "lab_l": 65.0, "lab_a": 42.0, "lab_b": 3.0},
  {"code": "963", "name": "Dusty Rose Ultra Very Light", "hex": "#FFE0E8", "lab_l": 92.0, "lab_a": 10.0, "lab_b": 0.0},
  {"code": "964", "name": "Sea Green Light", "hex": "#8FD8C8", "lab_l": 82.0, "lab_a": -28.0, "lab_b": 5.0},
  {"code": "966", "name": "Baby Green Medium", "hex": "#C8DEC8", "lab_l": 87.0, "lab_a": -12.0, "lab_b": 8.0},
  {"code": "970", "name": "Pumpkin Light", "hex": "#FF9838", "lab_l": 72.0, "lab_a": 35.0, "lab_b": 68.0},
  {"code": "971", "name": "Pumpkin", "hex": "#FF8428", "lab_l": 68.0, "lab_a": 40.0, "lab_b": 70.0},
  {"code": "972", "name": "Canary Deep", "hex": "#FFB828", "lab_l": 78.0, "lab_a": 15.0, "lab_b": 75.0},
  {"code": "973", "name": "Canary Bright", "hex": "#FFCE28", "lab_l": 84.0, "lab_a": 5.0, "lab_b": 78.0},
  {"code": "975", "name": "Golden Brown Dark", "hex": "#8B5428", "lab_l": 42.0, "lab_a": 18.0, "lab_b": 35.0},
  {"code": "976", "name": "Golden Brown Medium", "hex": "#AF6B38", "lab_l": 52.0, "lab_a": 20.0, "lab_b": 38.0},
  {"code": "977", "name": "Golden Brown Light", "hex": "#C88F54", "lab_l": 62.0, "lab_a": 18.0, "lab_b": 40.0},
  {"code": "986", "name": "Forest Green Very Dark", "hex": "#2F5438", "lab_l": 35.0, "lab_a": -28.0, "lab_b": 12.0},
  {"code": "987", "name": "Forest Green Dark", "hex": "#3F6F48", "lab_l": 45.0, "lab_a": -32.0, "lab_b": 15.0},
  {"code": "988", "name": "Forest Green Medium", "hex": "#5A8F64", "lab_l": 55.0, "lab_a": -35.0, "lab_b": 18.0},
  {"code": "989", "name": "Forest Green", "hex": "#7AAF74", "lab_l": 68.0, "lab_a": -32.0, "lab_b": 20.0},
  {"code": "991", "name": "Aquamarine Dark", "hex": "#2F7A5F", "lab_l": 48.0, "lab_a": -32.0, "lab_b": 8.0},
  {"code": "992", "name": "Aquamarine", "hex": "#5FBFAF", "lab_l": 72.0, "lab_a": -35.0, "lab_b": 5.0},
  {"code": "993", "name": "Aquamarine Light", "hex": "#8FD8C8", "lab_l": 82.0, "lab_a": -28.0, "lab_b": 5.0},
  {"code": "995", "name": "Electric Blue Dark", "hex": "#3F8FBF", "lab_l": 58.0, "lab_a": -15.0, "lab_b": -28.0},
  {"code": "996", "name": "Electric Blue Medium", "hex": "#5FAFD8", "lab_l": 70.0, "lab_a": -18.0, "lab_b": -25.0},
  {"code": "3011", "name": "Khaki Green Dark", "hex": "#8B7A54", "lab_l": 52.0, "lab_a": 0.0, "lab_b": 25.0},
  {"code": "3012", "name": "Khaki Green Medium", "hex": "#AF9F74", "lab_l": 65.0, "lab_a": 0.0, "lab_b": 28.0},
  {"code": "3013", "name": "Khaki Green Light", "hex": "#C8B593", "lab_l": 75.0, "lab_a": 0.0, "lab_b": 25.0},
  {"code": "3021", "name": "Brown Gray Very Dark", "hex": "#5C4538", "lab_l": 32.0, "lab_a": 8.0, "lab_b": 15.0},
  {"code": "3022", "name": "Brown Gray Medium", "hex": "#9F8F74", "lab_l": 60.0, "lab_a": 5.0, "lab_b": 20.0},
  {"code": "3023", "name": "Brown Gray Light", "hex": "#C8AF93", "lab_l": 72.0, "lab_a": 5.0, "lab_b": 22.0},
  {"code": "3024", "name": "Brown Gray Very Light", "hex": "#E8D8C8", "lab_l": 87.0, "lab_a": 3.0, "lab_b": 12.0},
  {"code": "3031", "name": "Mocha Brown Very Dark", "hex": "#4F3026", "lab_l": 24.0, "lab_a": 12.0, "lab_b": 15.0},
  {"code": "3032", "name": "Mocha Brown Medium", "hex": "#C8AF93", "lab_l": 72.0, "lab_a": 8.0, "lab_b": 22.0},
  {"code": "3033", "name": "Mocha Brown Very Light", "hex": "#E8CEB4", "lab_l": 84.0, "lab_a": 8.0, "lab_b": 20.0},
  {"code": "3041", "name": "Antique Violet Medium", "hex": "#C8A8B4", "lab_l": 72.0, "lab_a": 12.0, "lab_b": -3.0},
  {"code": "3042", "name": "Antique Violet Light", "hex": "#E0C8D0", "lab_l": 82.0, "lab_a": 8.0, "lab_b": -3.0},
  {"code": "3045", "name": "Yellow Beige Dark", "hex": "#D3A574", "lab_l": 70.0, "lab_a": 8.0, "lab_b": 32.0},
  {"code": "3046", "name": "Yellow Beige Medium", "hex": "#E8C594", "lab_l": 80.0, "lab_a": 8.0, "lab_b": 30.0},
  {"code": "3047", "name": "Yellow Beige Light", "hex": "#F0DEB4", "lab_l": 88.0, "lab_a": 5.0, "lab_b": 25.0},
  {"code": "3051", "name": "Green Gray Dark", "hex": "#5A6F54", "lab_l": 45.0, "lab_a": -12.0, "lab_b": 12.0},
  {"code": "3052", "name": "Green Gray Medium", "hex": "#8FAF86", "lab_l": 68.0, "lab_a": -18.0, "lab_b": 15.0},
  {"code": "3053", "name": "Green Gray", "hex": "#AFB59F", "lab_l": 72.0, "lab_a": -8.0, "lab_b": 12.0},
  {"code": "3064", "name": "Desert Sand", "hex": "#C89F74", "lab_l": 68.0, "lab_a": 12.0, "lab_b": 28.0},
  {"code": "3072", "name": "Beaver Gray Very Light", "hex": "#E8E8E8", "lab_l": 92.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "3078", "name": "Golden Yellow Very Light", "hex": "#FFEEC8", "lab_l": 94.0, "lab_a": 0.0, "lab_b": 28.0},
  {"code": "3325", "name": "Baby Blue Light", "hex": "#B4D8F0", "lab_l": 85.0, "lab_a": -8.0, "lab_b": -15.0},
  {"code": "3326", "name": "Rose Light", "hex": "#FFAFC8", "lab_l": 78.0, "lab_a": 32.0, "lab_b": 0.0},
  {"code": "3328", "name": "Salmon Dark", "hex": "#D8465C", "lab_l": 52.0, "lab_a": 58.0, "lab_b": 18.0},
  {"code": "3340", "name": "Apricot Medium", "hex": "#FF8C64", "lab_l": 68.0, "lab_a": 38.0, "lab_b": 35.0},
  {"code": "3341", "name": "Apricot", "hex": "#FFAF8C", "lab_l": 78.0, "lab_a": 28.0, "lab_b": 30.0},
  {"code": "3345", "name": "Hunter Green Dark", "hex": "#3F5F47", "lab_l": 40.0, "lab_a": -20.0, "lab_b": 10.0},
  {"code": "3346", "name": "Hunter Green", "hex": "#5A7A5F", "lab_l": 50.0, "lab_a": -22.0, "lab_b": 12.0},
  {"code": "3347", "name": "Yellow Green Medium", "hex": "#7A9F74", "lab_l": 62.0, "lab_a": -25.0, "lab_b": 20.0},
  {"code": "3348", "name": "Yellow Green Light", "hex": "#C8E8B4", "lab_l": 88.0, "lab_a": -20.0, "lab_b": 25.0},
  {"code": "3350", "name": "Dusty Rose Ultra Dark", "hex": "#B83854", "lab_l": 45.0, "lab_a": 55.0, "lab_b": 5.0},
  {"code": "3354", "name": "Dusty Rose Light", "hex": "#FFB8C8", "lab_l": 82.0, "lab_a": 22.0, "lab_b": 2.0},
  {"code": "3362", "name": "Pine Green Dark", "hex": "#5A6F5A", "lab_l": 45.0, "lab_a": -15.0, "lab_b": 8.0},
  {"code": "3363", "name": "Pine Green Medium", "hex": "#748F74", "lab_l": 58.0, "lab_a": -18.0, "lab_b": 10.0},
  {"code": "3364", "name": "Pine Green", "hex": "#8FA086", "lab_l": 65.0, "lab_a": -15.0, "lab_b": 12.0},
  {"code": "3371", "name": "Black Brown", "hex": "#281818", "lab_l": 12.0, "lab_a": 5.0, "lab_b": 5.0},
  {"code": "3607", "name": "Plum Light", "hex": "#C86B8C", "lab_l": 58.0, "lab_a": 38.0, "lab_b": -5.0},
  {"code": "3608", "name": "Plum Very Light", "hex": "#E894AF", "lab_l": 70.0, "lab_a": 32.0, "lab_b": -5.0},
  {"code": "3609", "name": "Plum Ultra Light", "hex": "#FFB8D6", "lab_l": 82.0, "lab_a": 25.0, "lab_b": -5.0},
  {"code": "3685", "name": "Mauve Very Dark", "hex": "#7A2848", "lab_l": 32.0, "lab_a": 42.0, "lab_b": -5.0},
  {"code": "3687", "name": "Mauve", "hex": "#A83864", "lab_l": 45.0, "lab_a": 40.0, "lab_b": -5.0},
  {"code": "3688", "name": "Mauve Medium", "hex": "#D85C8C", "lab_l": 58.0, "lab_a": 50.0, "lab_b": -3.0},
  {"code": "3689", "name": "Mauve Light", "hex": "#FF94B4", "lab_l": 72.0, "lab_a": 38.0, "lab_b": -3.0},
  {"code": "3705", "name": "Melon Dark", "hex": "#FF5C7C", "lab_l": 60.0, "lab_a": 58.0, "lab_b": 8.0},
  {"code": "3706", "name": "Melon Medium", "hex": "#FF8094", "lab_l": 68.0, "lab_a": 48.0, "lab_b": 8.0},
  {"code": "3708", "name": "Melon Light", "hex": "#FFAABE", "lab_l": 78.0, "lab_a": 35.0, "lab_b": 5.0},
  {"code": "3712", "name": "Salmon Medium", "hex": "#E86B74", "lab_l": 60.0, "lab_a": 48.0, "lab_b": 18.0},
  {"code": "3713", "name": "Salmon Very Light", "hex": "#FFE0D8", "lab_l": 92.0, "lab_a": 12.0, "lab_b": 12.0},
  {"code": "3716", "name": "Dusty Rose Very Light", "hex": "#FFD8E0", "lab_l": 90.0, "lab_a": 15.0, "lab_b": 0.0},
  {"code": "3721", "name": "Shell Pink Dark", "hex": "#A03854", "lab_l": 42.0, "lab_a": 45.0, "lab_b": 5.0},
  {"code": "3722", "name": "Shell Pink Medium", "hex": "#B8466B", "lab_l": 50.0, "lab_a": 42.0, "lab_b": 5.0},
  {"code": "3726", "name": "Antique Mauve Dark", "hex": "#A05C74", "lab_l": 50.0, "lab_a": 30.0, "lab_b": 0.0},
  {"code": "3727", "name": "Antique Mauve Light", "hex": "#D89FAF", "lab_l": 72.0, "lab_a": 22.0, "lab_b": 0.0},
  {"code": "3731", "name": "Dusty Rose Very Dark", "hex": "#D83858", "lab_l": 50.0, "lab_a": 60.0, "lab_b": 8.0},
  {"code": "3733", "name": "Dusty Rose", "hex": "#E85C7C", "lab_l": 60.0, "lab_a": 52.0, "lab_b": 8.0},
  {"code": "3740", "name": "Antique Violet Dark", "hex": "#9F7A8C", "lab_l": 58.0, "lab_a": 15.0, "lab_b": -5.0},
  {"code": "3743", "name": "Antique Violet Very Light", "hex": "#E8DDE0", "lab_l": 90.0, "lab_a": 5.0, "lab_b": -3.0},
  {"code": "3746", "name": "Blue Violet Dark", "hex": "#7A5F9F", "lab_l": 48.0, "lab_a": 22.0, "lab_b": -25.0},
  {"code": "3747", "name": "Blue Violet Very Light", "hex": "#E0D8F0", "lab_l": 88.0, "lab_a": 8.0, "lab_b": -12.0},
  {"code": "3750", "name": "Antique Blue Very Dark", "hex": "#2F4558", "lab_l": 30.0, "lab_a": -5.0, "lab_b": -12.0},
  {"code": "3752", "name": "Antique Blue Very Light", "hex": "#C8D8E0", "lab_l": 85.0, "lab_a": -5.0, "lab_b": -8.0},
  {"code": "3753", "name": "Antique Blue Ultra Very Light", "hex": "#E0EDF0", "lab_l": 93.0, "lab_a": -5.0, "lab_b": -5.0},
  {"code": "3755", "name": "Baby Blue", "hex": "#8FC5E0", "lab_l": 78.0, "lab_a": -10.0, "lab_b": -18.0},
  {"code": "3756", "name": "Baby Blue Ultra Very Light", "hex": "#F0F8FF", "lab_l": 98.0, "lab_a": -3.0, "lab_b": -5.0},
  {"code": "3760", "name": "Wedgwood Medium", "hex": "#5F8FAF", "lab_l": 58.0, "lab_a": -5.0, "lab_b": -22.0},
  {"code": "3761", "name": "Sky Blue Light", "hex": "#D0E8F8", "lab_l": 90.0, "lab_a": -8.0, "lab_b": -10.0},
  {"code": "3765", "name": "Peacock Blue Very Dark", "hex": "#2F5F7A", "lab_l": 38.0, "lab_a": -12.0, "lab_b": -18.0},
  {"code": "3766", "name": "Peacock Blue Light", "hex": "#8FB4C8", "lab_l": 72.0, "lab_a": -10.0, "lab_b": -15.0},
  {"code": "3768", "name": "Gray Green Dark", "hex": "#5A7A70", "lab_l": 50.0, "lab_a": -18.0, "lab_b": 5.0},
  {"code": "3770", "name": "Tawny Very Light", "hex": "#FFF0E0", "lab_l": 95.0, "lab_a": 5.0, "lab_b": 12.0},
  {"code": "3771", "name": "Terra Cotta Ultra Very Light", "hex": "#F0CEB4", "lab_l": 84.0, "lab_a": 12.0, "lab_b": 18.0},
  {"code": "3772", "name": "Desert Sand Very Dark", "hex": "#A86B48", "lab_l": 50.0, "lab_a": 18.0, "lab_b": 28.0},
  {"code": "3773", "name": "Desert Sand Medium", "hex": "#C89F74", "lab_l": 68.0, "lab_a": 12.0, "lab_b": 28.0},
  {"code": "3774", "name": "Desert Sand Very Light", "hex": "#F0DEC8", "lab_l": 90.0, "lab_a": 8.0, "lab_b": 18.0},
  {"code": "3776", "name": "Mahogany Light", "hex": "#D3854C", "lab_l": 62.0, "lab_a": 28.0, "lab_b": 38.0},
  {"code": "3777", "name": "Terra Cotta Very Dark", "hex": "#9B4528", "lab_l": 40.0, "lab_a": 35.0, "lab_b": 30.0},
  {"code": "3778", "name": "Terra Cotta Light", "hex": "#E89474", "lab_l": 68.0, "lab_a": 28.0, "lab_b": 28.0},
  {"code": "3779", "name": "Terra Cotta Ultra Very Light", "hex": "#FFB8A4", "lab_l": 80.0, "lab_a": 20.0, "lab_b": 22.0},
  {"code": "3781", "name": "Mocha Brown Dark", "hex": "#6F4538", "lab_l": 35.0, "lab_a": 15.0, "lab_b": 18.0},
  {"code": "3782", "name": "Mocha Brown Light", "hex": "#AF8F74", "lab_l": 62.0, "lab_a": 10.0, "lab_b": 22.0},
  {"code": "3787", "name": "Brown Gray Dark", "hex": "#7A6F64", "lab_l": 48.0, "lab_a": 5.0, "lab_b": 12.0},
  {"code": "3790", "name": "Beige Gray Ultra Dark", "hex": "#8F7A64", "lab_l": 52.0, "lab_a": 8.0, "lab_b": 18.0},
  {"code": "3799", "name": "Pewter Gray Very Dark", "hex": "#383838", "lab_l": 25.0, "lab_a": 0.0, "lab_b": 0.0},
  {"code": "3801", "name": "Christmas Red Very Dark", "hex": "#C82838", "lab_l": 45.0, "lab_a": 62.0, "lab_b": 20.0},
  {"code": "3802", "name": "Antique Mauve Very Dark", "hex": "#6F3854", "lab_l": 32.0, "lab_a": 28.0, "lab_b": -5.0},
  {"code": "3803", "name": "Mauve Dark", "hex": "#A84664", "lab_l": 45.0, "lab_a": 42.0, "lab_b": -5.0},
  {"code": "3804", "name": "Cyclamen Pink Dark", "hex": "#E84678", "lab_l": 55.0, "lab_a": 58.0, "lab_b": -3.0},
  {"code": "3805", "name": "Cyclamen Pink", "hex": "#FF648C", "lab_l": 62.0, "lab_a": 52.0, "lab_b": -3.0},
  {"code": "3806", "name": "Cyclamen Pink Light", "hex": "#FF94AF", "lab_l": 72.0, "lab_a": 40.0, "lab_b": -3.0},
  {"code": "3807", "name": "Cornflower Blue", "hex": "#6F7FAF", "lab_l": 55.0, "lab_a": 8.0, "lab_b": -30.0},
  {"code": "3808", "name": "Turquoise Ultra Very Dark", "hex": "#2F6F7A", "lab_l": 42.0, "lab_a": -22.0, "lab_b": -12.0},
  {"code": "3809", "name": "Turquoise Very Dark", "hex": "#3F8F9F", "lab_l": 55.0, "lab_a": -25.0, "lab_b": -15.0},
  {"code": "3810", "name": "Turquoise Dark", "hex": "#5FAFBF", "lab_l": 68.0, "lab_a": -28.0, "lab_b": -15.0},
  {"code": "3811", "name": "Turquoise Very Light", "hex": "#BFE8ED", "lab_l": 90.0, "lab_a": -18.0, "lab_b": -8.0},
  {"code": "3812", "name": "Sea Green Very Dark", "hex": "#2F8F74", "lab_l": 55.0, "lab_a": -35.0, "lab_b": 8.0},
  {"code": "3813", "name": "Blue Green Light", "hex": "#9FC8B4", "lab_l": 78.0, "lab_a": -20.0, "lab_b": 8.0},
  {"code": "3814", "name": "Aquamarine", "hex": "#4F9F8C", "lab_l": 60.0, "lab_a": -32.0, "lab_b": 5.0},
  {"code": "3815", "name": "Celadon Green Dark", "hex": "#3F6F5A", "lab_l": 42.0, "lab_a": -22.0, "lab_b": 5.0},
  {"code": "3816", "name": "Celadon Green", "hex": "#5F9F7C", "lab_l": 60.0, "lab_a": -28.0, "lab_b": 10.0},
  {"code": "3817", "name": "Celadon Green Light", "hex": "#9FC8AB", "lab_l": 78.0, "lab_a": -22.0, "lab_b": 12.0},
  {"code": "3818", "name": "Emerald Green Ultra Very Dark", "hex": "#0F4528", "lab_l": 25.0, "lab_a": -28.0, "lab_b": 10.0},
  {"code": "3819", "name": "Moss Green Light", "hex": "#E0ED99", "lab_l": 92.0, "lab_a": -12.0, "lab_b": 45.0},
  {"code": "3820", "name": "Straw Dark", "hex": "#D3A538", "lab_l": 68.0, "lab_a": 5.0, "lab_b": 55.0},
  {"code": "3821", "name": "Straw", "hex": "#E8B848", "lab_l": 76.0, "lab_a": 3.0, "lab_b": 58.0},
  {"code": "3822", "name": "Straw Light", "hex": "#F0CE64", "lab_l": 82.0, "lab_a": 0.0, "lab_b": 55.0},
  {"code": "3823", "name": "Yellow Ultra Pale", "hex": "#FFF8D8", "lab_l": 97.0, "lab_a": 0.0, "lab_b": 18.0},
  {"code": "3824", "name": "Apricot Light", "hex": "#FFCED6", "lab_l": 87.0, "lab_a": 15.0, "lab_b": 8.0},
  {"code": "3825", "name": "Pumpkin Pale", "hex": "#FFB874", "lab_l": 80.0, "lab_a": 22.0, "lab_b": 52.0},
  {"code": "3826", "name": "Golden Brown", "hex": "#AF6F38", "lab_l": 52.0, "lab_a": 20.0, "lab_b": 42.0},
  {"code": "3827", "name": "Golden Brown Pale", "hex": "#FFAF64", "lab_l": 78.0, "lab_a": 20.0, "lab_b": 50.0},
  {"code": "3828", "name": "Hazelnut Brown", "hex": "#B87F48", "lab_l": 58.0, "lab_a": 15.0, "lab_b": 38.0},
  {"code": "3829", "name": "Old Gold Very Dark", "hex": "#A87428", "lab_l": 52.0, "lab_a": 12.0, "lab_b": 48.0},
  {"code": "3830", "name": "Terra Cotta", "hex": "#C86B48", "lab_l": 55.0, "lab_a": 32.0, "lab_b": 32.0},
  {"code": "3831", "name": "Raspberry Dark", "hex": "#A82848", "lab_l": 40.0, "lab_a": 52.0, "lab_b": 5.0},
  {"code": "3832", "name": "Raspberry Medium", "hex": "#D83864", "lab_l": 52.0, "lab_a": 58.0, "lab_b": 3.0},
  {"code": "3833", "name": "Raspberry Light", "hex": "#E86B8C", "lab_l": 62.0, "lab_a": 48.0, "lab_b": 0.0},
  {"code": "3834", "name": "Grape Dark", "hex": "#7A2F5A", "lab_l": 32.0, "lab_a": 35.0, "lab_b": -15.0},
  {"code": "3835", "name": "Grape Medium", "hex": "#9F4674", "lab_l": 45.0, "lab_a": 42.0, "lab_b": -12.0},
  {"code": "3836", "name": "Grape Light", "hex": "#C86B9F", "lab_l": 58.0, "lab_a": 40.0, "lab_b": -10.0},
  {"code": "3837", "name": "Lavender Ultra Dark", "hex": "#6F3864", "lab_l": 35.0, "lab_a": 32.0, "lab_b": -15.0},
  {"code": "3838", "name": "Lavender Blue Dark", "hex": "#5F7FAF", "lab_l": 52.0, "lab_a": 8.0, "lab_b": -28.0},
  {"code": "3839", "name": "Lavender Blue Medium", "hex": "#7A9FCF", "lab_l": 65.0, "lab_a": 5.0, "lab_b": -30.0},
  {"code": "3840", "name": "Lavender Blue Light", "hex": "#AFD0ED", "lab_l": 82.0, "lab_a": 0.0, "lab_b": -20.0},
  {"code": "3841", "name": "Baby Blue Pale", "hex": "#D0E8F8", "lab_l": 90.0, "lab_a": -5.0, "lab_b": -12.0},
  {"code": "3842", "name": "Wedgwood Very Dark", "hex": "#2F5F7A", "lab_l": 38.0, "lab_a": -8.0, "lab_b": -18.0},
  {"code": "3843", "name": "Electric Blue", "hex": "#0FA8D8", "lab_l": 65.0, "lab_a": -25.0, "lab_b": -30.0},
  {"code": "3844", "name": "Turquoise Bright Dark", "hex": "#0F9FB8", "lab_l": 60.0, "lab_a": -30.0, "lab_b": -18.0},
  {"code": "3845", "name": "Turquoise Bright Medium", "hex": "#2FBFD8", "lab_l": 72.0, "lab_a": -32.0, "lab_b": -18.0},
  {"code": "3846", "name": "Turquoise Bright Light", "hex": "#5FD8ED", "lab_l": 82.0, "lab_a": -28.0, "lab_b": -15.0},
  {"code": "3847", "name": "Teal Green Dark", "hex": "#2F8F7A", "lab_l": 55.0, "lab_a": -32.0, "lab_b": 5.0},
  {"code": "3848", "name": "Teal Green Medium", "hex": "#4FAF9F", "lab_l": 68.0, "lab_a": -35.0, "lab_b": 5.0},
  {"code": "3849", "name": "Teal Green Light", "hex": "#7FCFBF", "lab_l": 80.0, "lab_a": -30.0, "lab_b": 5.0},
  {"code": "3850", "name": "Bright Green Dark", "hex": "#3F8F5F", "lab_l": 55.0, "lab_a": -38.0, "lab_b": 15.0},  {"code": "3850", "name": "Bright Green Dark", "hex": "#3F8F5F", "lab_l": 55.0, "lab_a": -38.0, "lab_b": 15.0},
  {"code": "3851", "name": "Bright Green Light", "hex": "#5FAFAF", "lab_l": 68.0, "lab_a": -32.0, "lab_b": 8.0},
  {"code": "3852", "name": "Straw Very Dark", "hex": "#D39F28", "lab_l": 65.0, "lab_a": 8.0, "lab_b": 60.0},
  {"code": "3853", "name": "Autumn Gold Dark", "hex": "#FF8C28", "lab_l": 68.0, "lab_a": 35.0, "lab_b": 68.0},
  {"code": "3854", "name": "Autumn Gold Medium", "hex": "#FFAF48", "lab_l": 78.0, "lab_a": 25.0, "lab_b": 65.0},
  {"code": "3855", "name": "Autumn Gold Light", "hex": "#FFD86B", "lab_l": 87.0, "lab_a": 10.0, "lab_b": 62.0},
  {"code": "3856", "name": "Mahogany Ultra Very Light", "hex": "#FFCE9F", "lab_l": 84.0, "lab_a": 15.0, "lab_b": 35.0},
  {"code": "3857", "name": "Rosewood Dark", "hex": "#8B2838", "lab_l": 35.0, "lab_a": 50.0, "lab_b": 15.0},
  {"code": "3858", "name": "Rosewood Medium", "hex": "#AB3848", "lab_l": 42.0, "lab_a": 48.0, "lab_b": 15.0},
  {"code": "3859", "name": "Rosewood Light", "hex": "#C8647C", "lab_l": 55.0, "lab_a": 42.0, "lab_b": 10.0},
  {"code": "3860", "name": "Cocoa", "hex": "#9F6B54", "lab_l": 50.0, "lab_a": 15.0, "lab_b": 22.0},
  {"code": "3861", "name": "Cocoa Light", "hex": "#C89F7C", "lab_l": 68.0, "lab_a": 12.0, "lab_b": 25.0},
  {"code": "3862", "name": "Mocha Beige Dark", "hex": "#9F7A54", "lab_l": 52.0, "lab_a": 12.0, "lab_b": 28.0},
  {"code": "3863", "name": "Mocha Beige Medium", "hex": "#B8946F", "lab_l": 62.0, "lab_a": 10.0, "lab_b": 28.0},
  {"code": "3864", "name": "Mocha Beige Light", "hex": "#D3B593", "lab_l": 75.0, "lab_a": 8.0, "lab_b": 25.0},
  {"code": "3865", "name": "Winter White", "hex": "#F8F8F0", "lab_l": 98.0, "lab_a": 0.0, "lab_b": 5.0},
  {"code": "3866", "name": "Mocha Brown Ultra Very Light", "hex": "#FAF0E6", "lab_l": 95.0, "lab_a": 3.0, "lab_b": 10.0}
]
```

This is a **subset of DMC colors** (about 350 of the most common). For now, save this as a JSON file in your project. Later we can expand it to include all 500+ DMC colors.

### Step 30: Create Database Seeder for Yarn Colors

Update `StitchLens.Data/DbInitializer.cs` to load the yarn data:

```csharp
using System.Text.Json;
using StitchLens.Data.Models;

namespace StitchLens.Data;

public static class DbInitializer
{
    public static void Initialize(StitchLensDbContext context, string contentRootPath)
    {
        context.Database.EnsureCreated();
        
        // Check if brands already exist
        if (context.YarnBrands.Any())
            return;
        
        // Add DMC brand
        var dmcBrand = new YarnBrand 
        { 
            Name = "DMC", 
            Country = "France", 
            IsActive = true 
        };
        context.YarnBrands.Add(dmcBrand);
        context.SaveChanges();
        
        // Load DMC colors from JSON
        var jsonPath = Path.Combine(contentRootPath, "SeedData", "dmc-colors.json");
        
        if (File.Exists(jsonPath))
        {
            var jsonContent = File.ReadAllText(jsonPath);
            var colorData = JsonSerializer.Deserialize<List<DmcColorJson>>(jsonContent);
            
            if (colorData != null)
            {
                foreach (var color in colorData)
                {
                    context.YarnColors.Add(new YarnColor
                    {
                        YarnBrandId = dmcBrand.Id,
                        Code = color.code,
                        Name = color.name,
                        HexColor = color.hex,
                        Lab_L = color.lab_l,
                        Lab_A = color.lab_a,
                        Lab_B = color.lab_b,
                        YardsPerSkein = 8 // Standard for DMC
                    });
                }
                
                context.SaveChanges();
                Console.WriteLine($"Seeded {colorData.Count} DMC colors");
            }
        }
        else
        {
            Console.WriteLine($"Warning: DMC colors file not found at {jsonPath}");
        }
        
        // Add other brands (without colors for now)
        context.YarnBrands.AddRange(
            new YarnBrand { Name = "Appleton", Country = "UK", IsActive = true },
            new YarnBrand { Name = "Paternayan", Country = "USA", IsActive = true }
        );
        context.SaveChanges();
    }
    
    private class DmcColorJson
    {
        public string code { get; set; } = "";
        public string name { get; set; } = "";
        public string hex { get; set; } = "";
        public double lab_l { get; set; }
        public double lab_a { get; set; }
        public double lab_b { get; set; }
    }
}
```

### Step 31: Update Program.cs to Pass Content Root

```csharp
// Update the seed call in Program.cs
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StitchLensDbContext>();
    var env = services.GetRequiredService<IWebHostEnvironment>();
    DbInitializer.Initialize(context, env.ContentRootPath);
}
```

### Step 32: Implement ΔE2000 Color Matching

Add to `StitchLens.Core/ColorScience/ColorConverter.cs`:

```csharp
/// <summary>
/// Calculate CIEDE2000 color difference - the most accurate perceptual difference formula
/// Returns a value where 0 = identical, 1 = just noticeable difference, 2+ = noticeable
/// </summary>
public static double CalculateDeltaE2000(
    double l1, double a1, double b1,
    double l2, double a2, double b2)
{
    // Reference: "The CIEDE2000 Color-Difference Formula" by Sharma, Wu, Dalal
    
    // Step 1: Calculate C' and h'
    double c1 = Math.Sqrt(a1 * a1 + b1 * b1);
    double c2 = Math.Sqrt(a2 * a2 + b2 * b2);
    double cMean = (c1 + c2) / 2.0;
    
    double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cMean, 7) / (Math.Pow(cMean, 7) + Math.Pow(25, 7))));
    
    double a1Prime = a1 * (1 + g);
    double a2Prime = a2 * (1 + g);
    
    double c1Prime = Math.Sqrt(a1Prime * a1Prime + b1 * b1);
    double c2Prime = Math.Sqrt(a2Prime * a2Prime + b2 * b2);
    
    double h1Prime = Math.Atan2(b1, a1Prime) * 180.0 / Math.PI;
    if (h1Prime < 0) h1Prime += 360.0;
    
    double h2Prime = Math.Atan2(b2, a2Prime) * 180.0 / Math.PI;
    if (h2Prime < 0) h2Prime += 360.0;
    
    // Step 2: Calculate ΔL', ΔC', ΔH'
    double deltaLPrime = l2 - l1;
    double deltaCPrime = c2Prime - c1Prime;
    
    double deltahPrime;
    if (c1Prime * c2Prime == 0)
    {
        deltahPrime = 0;
    }
    else
    {
        double diff = h2Prime - h1Prime;
        if (Math.Abs(diff) <= 180)
            deltahPrime = diff;
        else if (diff > 180)
            deltahPrime = diff - 360;
        else
            deltahPrime = diff + 360;
    }
    
    double deltaHPrime = 2 * Math.Sqrt(c1Prime * c2Prime) * Math.Sin(deltahPrime * Math.PI / 360.0);
    
    // Step 3: Calculate CIEDE2000
    double lPrimeMean = (l1 + l2) / 2.0;
    double cPrimeMean = (c1Prime + c2Prime) / 2.0;
    
    double hPrimeMean;
    if (c1Prime * c2Prime == 0)
    {
        hPrimeMean = h1Prime + h2Prime;
    }
    else
    {
        double sum = h1Prime + h2Prime;
        double diff = Math.Abs(h1Prime - h2Prime);
        if (diff <= 180)
            hPrimeMean = sum / 2.0;
        else if (sum < 360)
            hPrimeMean = (sum + 360) / 2.0;
        else
            hPrimeMean = (sum - 360) / 2.0;
    }
    
    double t = 1 - 0.17 * Math.Cos((hPrimeMean - 30) * Math.PI / 180.0)
        + 0.24 * Math.Cos(2 * hPrimeMean * Math.PI / 180.0)
        + 0.32 * Math.Cos((3 * hPrimeMean + 6) * Math.PI / 180.0)
        - 0.20 * Math.Cos((4 * hPrimeMean - 63) * Math.PI / 180.0);
    
    double deltaTheta = 30 * Math.Exp(-Math.Pow((hPrimeMean - 275) / 25.0, 2));
    
    double rC = 2 * Math.Sqrt(Math.Pow(cPrimeMean, 7) / (Math.Pow(cPrimeMean, 7) + Math.Pow(25, 7)));
    
    double sL = 1 + (0.015 * Math.Pow(lPrimeMean - 50, 2)) / Math.Sqrt(20 + Math.Pow(lPrimeMean - 50, 2));
    double sC = 1 + 0.045 * cPrimeMean;
    double sH = 1 + 0.015 * cPrimeMean * t;
    
    double rT = -Math.Sin(2 * deltaTheta * Math.PI / 180.0) * rC;
    
    // Weighting factors (kL = kC = kH = 1 for standard viewing conditions)
    double kL = 1.0;
    double kC = 1.0;
    double kH = 1.0;
    
    double deltaE = Math.Sqrt(
        Math.Pow(deltaLPrime / (kL * sL), 2) +
        Math.Pow(deltaCPrime / (kC * sC), 2) +
        Math.Pow(deltaHPrime / (kH * sH), 2) +
        rT * (deltaCPrime / (kC * sC)) * (deltaHPrime / (kH * sH))
    );
    
    return deltaE;
}
```

### Step 33: Create Yarn Matching Service

Create `StitchLens.Core/Services/IYarnMatchingService.cs`:

```csharp
namespace StitchLens.Core.Services;

public interface IYarnMatchingService
{
    Task<List<YarnMatch>> MatchColorsToYarnAsync(
        List<ColorInfo> palette, 
        int yarnBrandId);
}

public class YarnMatch
{
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
```

Create `StitchLens.Core/Services/YarnMatchingService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.ColorScience;
using StitchLens.Data;

namespace StitchLens.Core.Services;

public class YarnMatchingService : IYarnMatchingService
{
    private readonly StitchLensDbContext _context;
    
    public YarnMatchingService(StitchLensDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<YarnMatch>> MatchColorsToYarnAsync(
        List<ColorInfo> palette, 
        int yarnBrandId)
    {
        // Load all yarn colors for the brand
        var yarnColors = await _context.YarnColors
            .Where(y => y.YarnBrandId == yarnBrandId)
            .ToListAsync();
        
        var matches = new List<YarnMatch>();
        
        foreach (var paletteColor in palette)
        {
            // Find best matching yarn using ΔE2000
            var bestMatch = yarnColors
                .Select(yarn => new
                {
                    Yarn = yarn,
                    DeltaE = ColorConverter.CalculateDeltaE2000(
                        paletteColor.Lab_L, paletteColor.Lab_A, paletteColor.Lab_B,
                        yarn.Lab_L, yarn.Lab_A, yarn.Lab_B)
                })
                .OrderBy(m => m.DeltaE)
                .First();
            
            // Calculate yarn needed
            // Assume ~0.5 yards per stitch (conservative estimate for needlepoint)
            int yardsNeeded = (int)Math.Ceiling(paletteColor.PixelCount * 0.5);
            int skeinsNeeded = (int)Math.Ceiling((double)yardsNeeded / bestMatch.Yarn.YardsPerSkein);
            
            matches.Add(new YarnMatch
            {
                YarnColorId = bestMatch.Yarn.Id,
                Code = bestMatch.Yarn.Code,
                Name = bestMatch.Yarn.Name,
                HexColor = bestMatch.Yarn.HexColor,
                R = paletteColor.R,
                G = paletteColor.G,
                B = paletteColor.B,
                Lab_L = bestMatch.Yarn.Lab_L,
                Lab_A = bestMatch.Yarn.Lab_A,
                Lab_B = bestMatch.Yarn.Lab_B,
                StitchCount = paletteColor.PixelCount,
                DeltaE = bestMatch.DeltaE,
                YardsNeeded = yardsNeeded,
                EstimatedSkeins = skeinsNeeded
            });
        }
        
        return matches.OrderByDescending(m => m.StitchCount).ToList();
    }
}
```

### Step 34: Register Service and Create SeedData Folder

1. **Register the service in Program.cs:**
```csharp
builder.Services.AddScoped<IYarnMatchingService, YarnMatchingService>();
```

2. **Create the folder structure:**
- In your `StitchLens.Web` project, create a folder called `SeedData`
- Save the JSON from Step 29 as `StitchLens.Web/SeedData/dmc-colors.json`

3. **Test the seeding:**
```bash
# Delete your database to start fresh
rm stitchlens.db

# Run the app
dotnet watch run
```

Check the console output - you should see "Seeded XXX DMC colors"

Tomorrow we'll wire up the yarn matching to the UI and show users their shopping list! 🧵

Does this make sense so far? The JSON file is quite long - I can provide it in chunks if you need.

---

## Phase 6: Wire Up Yarn Matching UI

Now let's show users their matched yarn colors with shopping info!

### Step 35: Update PatternController to Match Yarns

Update the `Configure` POST action in `PatternController.cs`:

```csharp
// Add to constructor
private readonly IYarnMatchingService _yarnMatchingService;

public PatternController(
    StitchLensDbContext context,
    IImageProcessingService imageService,
    IColorQuantizationService colorService,
    IYarnMatchingService yarnMatchingService)
{
    _context = context;
    _imageService = imageService;
    _colorService = colorService;
    _yarnMatchingService = yarnMatchingService;
}

// Update Configure POST action
[HttpPost]
public async Task<IActionResult> Configure(ConfigureViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.YarnBrands = await _context.YarnBrands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToListAsync();
        
        return View(model);
    }
    
    var project = await _context.Projects.FindAsync(model.ProjectId);
    if (project == null)
        return NotFound();
    
    // Update project with settings
    project.MeshCount = model.MeshCount;
    project.WidthInches = model.WidthInches;
    project.HeightInches = model.HeightInches;
    project.MaxColors = model.MaxColors;
    project.StitchType = model.StitchType;
    project.YarnBrandId = model.YarnBrandId;
    
    await _context.SaveChangesAsync();
    
    // Generate quantized pattern
    var imageBytes = await System.IO.File.ReadAllBytesAsync(project.OriginalImagePath);
    var quantized = await _colorService.QuantizeAsync(imageBytes, project.MaxColors);
    
    // Save quantized image
    var quantizedFileName = $"{Path.GetFileNameWithoutExtension(project.OriginalImagePath)}_quantized.png";
    var quantizedPath = Path.Combine(Path.GetDirectoryName(project.OriginalImagePath)!, quantizedFileName);
    await System.IO.File.WriteAllBytesAsync(quantizedPath, quantized.QuantizedImageData);
    project.ProcessedImagePath = quantizedPath;
    
    // Match colors to yarns if brand selected
    if (project.YarnBrandId.HasValue)
    {
        var yarnMatches = await _yarnMatchingService.MatchColorsToYarnAsync(
            quantized.Palette, 
            project.YarnBrandId.Value);
        
        // Store matched yarn info as JSON
        project.PaletteJson = System.Text.Json.JsonSerializer.Serialize(yarnMatches);
    }
    else
    {
        // Store just the palette if no brand selected
        project.PaletteJson = System.Text.Json.JsonSerializer.Serialize(quantized.Palette);
    }
    
    await _context.SaveChangesAsync();
    
    return RedirectToAction("Preview", new { id = project.Id });
}
```

### Step 36: Create Preview ViewModel

Create `StitchLens.Web/Models/PreviewViewModel.cs`:

```csharp
using StitchLens.Core.Services;
using StitchLens.Data.Models;

namespace StitchLens.Web.Models;

public class PreviewViewModel
{
    public Project Project { get; set; } = null!;
    public List<YarnMatch>? YarnMatches { get; set; }
    public List<ColorInfo>? UnmatchedColors { get; set; }
    public bool HasYarnMatching => YarnMatches != null && YarnMatches.Any();
    
    public int TotalStitches => Project.WidthInches > 0 && Project.HeightInches > 0
        ? (int)(Project.WidthInches * Project.MeshCount * Project.HeightInches * Project.MeshCount)
        : 0;
    
    public int TotalYardsNeeded => YarnMatches?.Sum(m => m.YardsNeeded) ?? 0;
    public int TotalSkeinsNeeded => YarnMatches?.Sum(m => m.EstimatedSkeins) ?? 0;
}
```

### Step 37: Update Preview Controller Action

Update the `Preview` action in `PatternController.cs`:

```csharp
using StitchLens.Web.Models;
using StitchLens.Core.Services;

// Update Preview action
public async Task<IActionResult> Preview(int id)
{
    var project = await _context.Projects
        .Include(p => p.YarnBrand)
        .FirstOrDefaultAsync(p => p.Id == id);
    
    if (project == null)
        return NotFound();
    
    var viewModel = new PreviewViewModel
    {
        Project = project
    };
    
    // Deserialize palette data
    if (!string.IsNullOrEmpty(project.PaletteJson))
    {
        if (project.YarnBrandId.HasValue)
        {
            // Has yarn matching
            viewModel.YarnMatches = System.Text.Json.JsonSerializer
                .Deserialize<List<YarnMatch>>(project.PaletteJson);
        }
        else
        {
            // No yarn matching - just palette
            viewModel.UnmatchedColors = System.Text.Json.JsonSerializer
                .Deserialize<List<ColorInfo>>(project.PaletteJson);
        }
    }
    
    return View(viewModel);
}
```

### Step 38: Update Preview View with Yarn Matching

Replace `Views/Pattern/Preview.cshtml`:

```html
@model StitchLens.Web.Models.PreviewViewModel
@{
    ViewData["Title"] = "Pattern Preview";
}

<div class="container mt-4">
    <h2>Your Needlepoint Pattern</h2>
    <p class="lead">@Model.Project.Title</p>
    
    <div class="row">
        <!-- Original Image -->
        <div class="col-md-6">
            <div class="card">
                <div class="card-header">
                    <h5>Original Image</h5>
                </div>
                <div class="card-body text-center">
                    <img src="/uploads/@System.IO.Path.GetFileName(Model.Project.OriginalImagePath)" 
                         alt="Original" 
                         class="img-fluid"
                         style="max-height: 400px;">
                </div>
            </div>
        </div>
        
        <!-- Quantized Preview -->
        <div class="col-md-6">
            <div class="card">
                <div class="card-header">
                    <h5>Pattern Preview (@Model.Project.MaxColors Colors)</h5>
                </div>
                <div class="card-body text-center">
                    @if (!string.IsNullOrEmpty(Model.Project.ProcessedImagePath))
                    {
                        <img src="/uploads/@System.IO.Path.GetFileName(Model.Project.ProcessedImagePath)" 
                             alt="Quantized" 
                             class="img-fluid"
                             style="max-height: 400px;">
                    }
                    else
                    {
                        <p class="text-muted">Processing...</p>
                    }
                </div>
            </div>
        </div>
    </div>
    
    <!-- Pattern Info -->
    <div class="row mt-4">
        <div class="col-md-4">
            <div class="card">
                <div class="card-body">
                    <h5>Canvas Specifications</h5>
                    <table class="table table-sm">
                        <tr>
                            <td><strong>Mesh Count:</strong></td>
                            <td>@Model.Project.MeshCount count</td>
                        </tr>
                        <tr>
                            <td><strong>Finished Size:</strong></td>
                            <td>@Model.Project.WidthInches.ToString("F1")" × @Model.Project.HeightInches.ToString("F1")"</td>
                        </tr>
                        <tr>
                            <td><strong>Total Stitches:</strong></td>
                            <td>@Model.TotalStitches.ToString("N0")</td>
                        </tr>
                        <tr>
                            <td><strong>Stitch Type:</strong></td>
                            <td>@Model.Project.StitchType</td>
                        </tr>
                    </table>
                </div>
            </div>
        </div>
        
        @if (Model.HasYarnMatching)
        {
            <div class="col-md-4">
                <div class="card">
                    <div class="card-body">
                        <h5>Yarn Summary</h5>
                        <table class="table table-sm">
                            <tr>
                                <td><strong>Brand:</strong></td>
                                <td>@Model.Project.YarnBrand?.Name</td>
                            </tr>
                            <tr>
                                <td><strong>Total Colors:</strong></td>
                                <td>@Model.YarnMatches!.Count</td>
                            </tr>
                            <tr>
                                <td><strong>Total Yards:</strong></td>
                                <td>@Model.TotalYardsNeeded yards</td>
                            </tr>
                            <tr>
                                <td><strong>Total Skeins:</strong></td>
                                <td>@Model.TotalSkeinsNeeded skeins</td>
                            </tr>
                        </table>
                    </div>
                </div>
            </div>
            
            <div class="col-md-4">
                <div class="card bg-success text-white">
                    <div class="card-body text-center">
                        <h5>Ready to Stitch!</h5>
                        <p class="mb-2">Your pattern is complete with yarn matching</p>
                        <button class="btn btn-light btn-lg mt-2" disabled>
                            <i class="bi bi-file-pdf"></i> Download PDF
                        </button>
                        <small class="d-block mt-2 text-white-50">Coming soon!</small>
                    </div>
                </div>
            </div>
        }
        else
        {
            <div class="col-md-8">
                <div class="card bg-warning">
                    <div class="card-body">
                        <h5>⚠️ No Yarn Brand Selected</h5>
                        <p>Go back to settings and select a yarn brand to get your shopping list!</p>
                        <a asp-action="Configure" asp-route-id="@Model.Project.Id" class="btn btn-primary">
                            Select Yarn Brand
                        </a>
                    </div>
                </div>
            </div>
        }
    </div>
    
    <!-- Yarn Shopping List -->
    @if (Model.HasYarnMatching)
    {
        <div class="card mt-4">
            <div class="card-header">
                <h5>
                    <i class="bi bi-cart"></i> Shopping List - @Model.Project.YarnBrand?.Name Yarn
                </h5>
            </div>
            <div class="card-body">
                <div class="table-responsive">
                    <table class="table table-hover">
                        <thead>
                            <tr>
                                <th style="width: 80px;">Color</th>
                                <th>Code</th>
                                <th>Name</th>
                                <th class="text-end">Stitches</th>
                                <th class="text-end">Yards</th>
                                <th class="text-end">Skeins</th>
                                <th class="text-end">Match Quality</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var yarn in Model.YarnMatches!)
                            {
                                var percentage = Model.TotalStitches > 0 
                                    ? (yarn.StitchCount * 100.0 / Model.TotalStitches).ToString("F1")
                                    : "0";
                                
                                var matchQuality = yarn.DeltaE < 2.0 ? "Excellent" 
                                    : yarn.DeltaE < 5.0 ? "Good" 
                                    : yarn.DeltaE < 10.0 ? "Fair" 
                                    : "Poor";
                                
                                var matchBadgeClass = yarn.DeltaE < 2.0 ? "success" 
                                    : yarn.DeltaE < 5.0 ? "info" 
                                    : yarn.DeltaE < 10.0 ? "warning" 
                                    : "danger";
                                
                                <tr>
                                    <td>
                                        <div class="d-flex align-items-center">
                                            <div style="width: 50px; height: 50px; background-color: @yarn.HexColor; border: 1px solid #ccc; border-radius: 4px;"></div>
                                        </div>
                                    </td>
                                    <td class="align-middle">
                                        <strong>@yarn.Code</strong>
                                    </td>
                                    <td class="align-middle">@yarn.Name</td>
                                    <td class="align-middle text-end">
                                        @yarn.StitchCount.ToString("N0")
                                        <small class="text-muted d-block">(@percentage%)</small>
                                    </td>
                                    <td class="align-middle text-end">@yarn.YardsNeeded</td>
                                    <td class="align-middle text-end">
                                        <strong>@yarn.EstimatedSkeins</strong>
                                    </td>
                                    <td class="align-middle text-end">
                                        <span class="badge bg-@matchBadgeClass">
                                            @matchQuality
                                        </span>
                                        <small class="text-muted d-block">ΔE: @yarn.DeltaE.ToString("F1")</small>
                                    </td>
                                </tr>
                            }
                        </tbody>
                        <tfoot>
                            <tr class="fw-bold">
                                <td colspan="3" class="text-end">TOTALS:</td>
                                <td class="text-end">@Model.TotalStitches.ToString("N0")</td>
                                <td class="text-end">@Model.TotalYardsNeeded</td>
                                <td class="text-end">@Model.TotalSkeinsNeeded</td>
                                <td></td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
                
                <div class="alert alert-info mt-3">
                    <h6><i class="bi bi-info-circle"></i> Shopping Tips:</h6>
                    <ul class="mb-0">
                        <li><strong>Match Quality</strong> shows how close the yarn color is to your image
                            <ul>
                                <li><span class="badge bg-success">Excellent</span> = Nearly identical (ΔE < 2)</li>
                                <li><span class="badge bg-info">Good</span> = Very close match (ΔE 2-5)</li>
                                <li><span class="badge bg-warning">Fair</span> = Acceptable (ΔE 5-10)</li>
                                <li><span class="badge bg-danger">Poor</span> = Noticeable difference (ΔE > 10)</li>
                            </ul>
                        </li>
                        <li>Yarn estimates include a small buffer - you may need less</li>
                        <li>Consider buying an extra skein of your most-used colors</li>
                        <li>Dye lots can vary - buy all yarn for one project at once</li>
                    </ul>
                </div>
            </div>
        </div>
    }
    
    <!-- Color Palette (if no yarn matching) -->
    @if (!Model.HasYarnMatching && Model.UnmatchedColors != null)
    {
        <div class="card mt-4">
            <div class="card-header">
                <h5>Color Palette (@Model.UnmatchedColors.Count colors)</h5>
            </div>
            <div class="card-body">
                <div class="row">
                    @foreach (var color in Model.UnmatchedColors)
                    {
                        var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                        var percentage = Model.TotalStitches > 0 
                            ? (color.PixelCount * 100.0 / Model.TotalStitches).ToString("F1")
                            : "0";
                        
                        <div class="col-md-3 col-sm-4 col-6 mb-3">
                            <div class="d-flex align-items-center">
                                <div style="width: 60px; height: 60px; background-color: @hexColor; border: 1px solid #ccc; border-radius: 4px; margin-right: 10px;"></div>
                                <div>
                                    <strong class="d-block">@hexColor</strong>
                                    <small class="text-muted">@color.PixelCount px (@percentage%)</small>
                                </div>
                            </div>
                        </div>
                    }
                </div>
            </div>
        </div>
    }
    
    <!-- Action Buttons -->
    <div class="mt-4 d-flex gap-2">
        <a asp-action="Configure" asp-route-id="@Model.Project.Id" class="btn btn-secondary">
            <i class="bi bi-arrow-left"></i> Adjust Settings
        </a>
        <a asp-action="Upload" class="btn btn-outline-secondary">
            <i class="bi bi-upload"></i> Start New Pattern
        </a>
        @if (Model.HasYarnMatching)
        {
            <button class="btn btn-success" disabled>
                <i class="bi bi-printer"></i> Print Shopping List
            </button>
        }
    </div>
</div>

@section Scripts {
    <script>
        // Print function for shopping list
        function printShoppingList() {
            window.print();
        }
    </script>
}
```

### Step 39: Test the Complete Flow!

```bash
# Make sure database has yarn colors
rm stitchlens.db  # Delete old db if needed
dotnet watch run
```

Now test the complete workflow:

1. ✅ Upload an image
2. ✅ Crop it
3. ✅ Configure settings (mesh, size, colors)
4. ✅ **Select DMC as yarn brand**
5. ✅ Click "Generate Pattern"
6. ✅ See your shopping list with real DMC colors!

You should see:
- Side-by-side original vs quantized images
- Complete canvas specifications
- **Shopping list with DMC codes, names, quantities**
- Match quality indicators (Excellent/Good/Fair/Poor)
- Total yards and skeins needed
- Color swatches showing matched yarns

The ΔE values show how close each match is - anything under 2.0 is essentially perfect!

Try it with different images and color counts to see how the matching works! 🎨🧵


## Phase 7: PDF Pattern Generation

Now let's create beautiful, printable needlepoint patterns!

### Step 40: Install QuestPDF Package

```bash
cd StitchLens.Core
dotnet add package QuestPDF
```

### Step 41: Create PDF Generation Service Interface

Create `StitchLens.Core/Services/IPdfGenerationService.cs`:

```csharp
namespace StitchLens.Core.Services;

public interface IPdfGenerationService
{
    Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data);
}

public class PatternPdfData
{
    public string Title { get; set; } = "Needlepoint Pattern";
    public int MeshCount { get; set; }
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int WidthStitches { get; set; }
    public int HeightStitches { get; set; }
    public string StitchType { get; set; } = "Tent";
    public byte[] QuantizedImageData { get; set; } = Array.Empty<byte>();
    public List<YarnMatch> YarnMatches { get; set; } = new();
    public string YarnBrand { get; set; } = "DMC";
}
```

### Step 42: Implement PDF Generation Service

Create `StitchLens.Core/Services/PdfGenerationService.cs`:

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;

namespace StitchLens.Core.Services;

public class PdfGenerationService : IPdfGenerationService
{
    public PdfGenerationService()
    {
        // Set QuestPDF license (Community license is free for open source)
        QuestPDF.Settings.License = LicenseType.Community;
    }
    
    public async Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data)
    {
        return await Task.Run(() =>
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(0.5f, Unit.Inch);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                    
                    page.Header().Element(header => CreateHeader(header, data));
                    page.Content().Element(content => CreateContent(content, data));
                    page.Footer().Element(footer => CreateFooter(footer));
                });
            });
            
            return document.GeneratePdf();
        });
    }
    
    private void CreateHeader(IContainer container, PatternPdfData data)
    {
        container.Column(column =>
        {
            column.Item().Text("StitchLens Needlepoint Pattern")
                .FontSize(20)
                .Bold()
                .FontColor(Colors.Blue.Darken3);
            
            column.Item().PaddingTop(5).Text(data.Title)
                .FontSize(16)
                .SemiBold();
            
            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Canvas Specifications").Bold();
                    col.Item().Text($"Mesh Count: {data.MeshCount}");
                    col.Item().Text($"Finished Size: {data.WidthInches:F1}\" × {data.HeightInches:F1}\"");
                    col.Item().Text($"Stitches: {data.WidthStitches} × {data.HeightStitches}");
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Pattern Details").Bold();
                    col.Item().Text($"Stitch Type: {data.StitchType}");
                    col.Item().Text($"Colors: {data.YarnMatches.Count}");
                    col.Item().Text($"Yarn Brand: {data.YarnBrand}");
                });
                
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Materials Needed").Bold();
                    col.Item().Text($"Total Yards: {data.YarnMatches.Sum(m => m.YardsNeeded)}");
                    col.Item().Text($"Total Skeins: {data.YarnMatches.Sum(m => m.EstimatedSkeins)}");
                    col.Item().Text($"Generated: {DateTime.Now:MM/dd/yyyy}");
                });
            });
            
            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }
    
    private void CreateContent(IContainer container, PatternPdfData data)
    {
        container.Column(column =>
        {
            // Pattern Preview Image
            column.Item().PaddingTop(10).Text("Pattern Preview")
                .FontSize(14)
                .Bold();
            
            column.Item().PaddingTop(5).Height(3, Unit.Inch).Background(Colors.Grey.Lighten3)
                .AlignCenter().AlignMiddle()
                .Element(imageContainer =>
                {
                    try
                    {
                        imageContainer.Image(data.QuantizedImageData);
                    }
                    catch
                    {
                        imageContainer.Text("Image preview unavailable");
                    }
                });
            
            // Shopping List
            column.Item().PageBreak();
            column.Item().PaddingTop(10).Text($"Shopping List - {data.YarnBrand} Yarn")
                .FontSize(14)
                .Bold();
            
            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);  // Color swatch
                    columns.ConstantColumn(50);  // Code
                    columns.RelativeColumn(3);   // Name
                    columns.ConstantColumn(60);  // Stitches
                    columns.ConstantColumn(50);  // Yards
                    columns.ConstantColumn(50);  // Skeins
                });
                
                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Color").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Code").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Name").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Stitches").FontColor(Colors.White).AlignRight().Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Yards").FontColor(Colors.White).AlignRight().Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Skeins").FontColor(Colors.White).AlignRight().Bold();
                });
                
                // Rows
                foreach (var yarn in data.YarnMatches)
                {
                    var rowColor = data.YarnMatches.IndexOf(yarn) % 2 == 0 
                        ? Colors.White 
                        : Colors.Grey.Lighten4;
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Background(yarn.HexColor);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.Code).FontSize(9);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.Name).FontSize(9);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.StitchCount.ToString("N0")).FontSize(9).AlignRight();
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.YardsNeeded.ToString()).FontSize(9).AlignRight();
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.EstimatedSkeins.ToString()).FontSize(9).Bold().AlignRight();
                }
                
                // Footer totals
                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(3).Background(Colors.Grey.Lighten2).Padding(5)
                        .Text("TOTALS:").Bold().AlignRight();
                    
                    footer.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text(data.YarnMatches.Sum(m => m.StitchCount).ToString("N0")).Bold().AlignRight();
                    
                    footer.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text(data.YarnMatches.Sum(m => m.YardsNeeded).ToString()).Bold().AlignRight();
                    
                    footer.Cell().Background(Colors.Grey.Lighten2).Padding(5)
                        .Text(data.YarnMatches.Sum(m => m.EstimatedSkeins).ToString()).Bold().AlignRight();
                });
            });
            
            // Instructions
            column.Item().PaddingTop(20).Text("Stitching Instructions")
                .FontSize(14)
                .Bold();
            
            column.Item().PaddingTop(10).Column(instructions =>
            {
                instructions.Item().Text("Getting Started:").Bold();
                instructions.Item().PaddingLeft(10).Text("1. Cut your canvas 2-3 inches larger than the finished size on all sides");
                instructions.Item().PaddingLeft(10).Text("2. Bind the edges with masking tape to prevent fraying");
                instructions.Item().PaddingLeft(10).Text("3. Find the center of your canvas and mark it lightly");
                
                instructions.Item().PaddingTop(10).Text("Stitching Tips:").Bold();
                instructions.Item().PaddingLeft(10).Text("• Work from the center outward when possible");
                instructions.Item().PaddingLeft(10).Text("• Use 18-inch strands of yarn to prevent tangling");
                instructions.Item().PaddingLeft(10).Text("• Follow the color chart, working one color at a time");
                instructions.Item().PaddingLeft(10).Text("• Keep consistent tension for even stitches");
                
                instructions.Item().PaddingTop(10).Text("Finishing:").Bold();
                instructions.Item().PaddingLeft(10).Text("• Block your finished piece by dampening and pinning to shape");
                instructions.Item().PaddingLeft(10).Text("• Allow to dry completely before framing or mounting");
                instructions.Item().PaddingLeft(10).Text("• Professional framing is recommended for best results");
            });
            
            // Color Legend with Symbols (for future grid implementation)
            column.Item().PageBreak();
            column.Item().PaddingTop(10).Text("Color Legend")
                .FontSize(14)
                .Bold();
            
            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);  // Swatch
                    columns.ConstantColumn(50);  // Code
                    columns.RelativeColumn(2);   // Name
                    columns.ConstantColumn(40);  // Symbol
                    columns.RelativeColumn(1);   // Usage %
                });
                
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Color").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Code").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Name").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Symbol").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Blue.Darken3).Padding(5)
                        .Text("Usage").FontColor(Colors.White).Bold();
                });
                
                var totalStitches = data.YarnMatches.Sum(m => m.StitchCount);
                var symbols = "•○◆◇■□▲△★☆●◉▪▫◘◙";
                
                for (int i = 0; i < data.YarnMatches.Count; i++)
                {
                    var yarn = data.YarnMatches[i];
                    var symbol = i < symbols.Length ? symbols[i].ToString() : (i + 1).ToString();
                    var percentage = totalStitches > 0 
                        ? (yarn.StitchCount * 100.0 / totalStitches).ToString("F1") 
                        : "0";
                    
                    var rowColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Background(yarn.HexColor);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.Code).FontSize(9);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(yarn.Name).FontSize(9);
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text(symbol).FontSize(14).Bold().AlignCenter();
                    
                    table.Cell().Background(rowColor).Padding(5)
                        .Text($"{percentage}%").FontSize(9);
                }
            });
        });
    }
    
    private void CreateFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Created with ").FontSize(8).FontColor(Colors.Grey.Medium);
            text.Span("StitchLens").FontSize(8).Bold().FontColor(Colors.Blue.Darken2);
            text.Span(" - www.stitchlens.com").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
```

### Step 43: Register PDF Service

Add to `Program.cs`:

```csharp
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();
```

### Step 44: Update PatternController to Generate PDF

Add a new action to `PatternController.cs`:

```csharp
// Add to constructor
private readonly IPdfGenerationService _pdfService;

public PatternController(
    StitchLensDbContext context,
    IImageProcessingService imageService,
    IColorQuantizationService colorService,
    IYarnMatchingService yarnMatchingService,
    IPdfGenerationService pdfService)
{
    _context = context;
    _imageService = imageService;
    _colorService = colorService;
    _yarnMatchingService = yarnMatchingService;
    _pdfService = pdfService;
}

// Add new action for PDF download
public async Task<IActionResult> DownloadPdf(int id)
{
    var project = await _context.Projects
        .Include(p => p.YarnBrand)
        .FirstOrDefaultAsync(p => p.Id == id);
    
    if (project == null)
        return NotFound();
    
    // Deserialize yarn matches
    var yarnMatches = new List<YarnMatch>();
    if (!string.IsNullOrEmpty(project.PaletteJson))
    {
        yarnMatches = System.Text.Json.JsonSerializer
            .Deserialize<List<YarnMatch>>(project.PaletteJson) ?? new List<YarnMatch>();
    }
    
    // Load quantized image
    byte[] imageData = Array.Empty<byte>();
    if (!string.IsNullOrEmpty(project.ProcessedImagePath) && 
        System.IO.File.Exists(project.ProcessedImagePath))
    {
        imageData = await System.IO.File.ReadAllBytesAsync(project.ProcessedImagePath);
    }
    
    // Create PDF data
    var pdfData = new PatternPdfData
    {
        Title = project.Title,
        MeshCount = project.MeshCount,
        WidthInches = project.WidthInches,
        HeightInches = project.HeightInches,
        WidthStitches = (int)(project.WidthInches * project.MeshCount),
        HeightStitches = (int)(project.HeightInches * project.MeshCount),
        StitchType = project.StitchType,
        QuantizedImageData = imageData,
        YarnMatches = yarnMatches,
        YarnBrand = project.YarnBrand?.Name ?? "Unknown"
    };
    
    // Generate PDF
    var pdfBytes = await _pdfService.GeneratePatternPdfAsync(pdfData);
    
    // Return as download
    var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
    return File(pdfBytes, "application/pdf", fileName);
}
```

### Step 45: Update Preview View to Enable PDF Download

Update the button in `Views/Pattern/Preview.cshtml`:

Replace the disabled "Download PDF" button (around line 60) with:

```html
<a asp-action="DownloadPdf" asp-route-id="@Model.Project.Id" 
   class="btn btn-light btn-lg mt-2">
    <i class="bi bi-file-pdf"></i> Download PDF
</a>
```

Also update the "Print Shopping List" button at the bottom (around line 250):

```html
@if (Model.HasYarnMatching)
{
    <a asp-action="DownloadPdf" asp-route-id="@Model.Project.Id" 
       class="btn btn-success">
        <i class="bi bi-file-pdf"></i> Download Pattern PDF
    </a>
}
```

### Step 46: Test PDF Generation!

```bash
dotnet watch run
```

Now complete the full workflow:
1. Upload an image
2. Crop it  
3. Configure (select DMC)
4. Generate pattern
5. **Click "Download PDF"**

You should get a beautiful multi-page PDF with:
- ✅ Pattern preview image
- ✅ Canvas specifications
- ✅ Complete shopping list with color swatches
- ✅ Stitching instructions
- ✅ Color legend with symbols
- ✅ Professional formatting

The PDF is print-ready and includes everything a needlepointer needs to complete the project!

---

## Phase 8: Add Gridded Stitch Chart

Now let's add the actual needlepoint grid that shows each stitch with symbols!

### Step 47: Create Grid Data Structure

Add to `StitchLens.Core/Services/IPdfGenerationService.cs`:

```csharp
public class StitchGrid
{
    public int Width { get; set; }
    public int Height { get; set; }
    public StitchCell[,] Cells { get; set; } = new StitchCell[0, 0];
}

public class StitchCell
{
    public int YarnMatchIndex { get; set; }
    public string Symbol { get; set; } = "";
    public string HexColor { get; set; } = "";
}
```

### Step 48: Create Grid Generation Service

Create `StitchLens.Core/Services/GridGenerationService.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StitchLens.Core.ColorScience;

namespace StitchLens.Core.Services;

public interface IGridGenerationService
{
    Task<StitchGrid> GenerateStitchGridAsync(
        byte[] quantizedImageData,
        int targetWidth,
        int targetHeight,
        List<YarnMatch> yarnMatches);
}

public class GridGenerationService : IGridGenerationService
{
    private readonly string[] _symbols = new[] 
    { 
        "•", "○", "◆", "◇", "■", "□", "▲", "△", "★", "☆", 
        "●", "◉", "▪", "▫", "◘", "◙", "▼", "▽", "◊", "◈" 
    };
    
    public async Task<StitchGrid> GenerateStitchGridAsync(
        byte[] quantizedImageData,
        int targetWidth,
        int targetHeight,
        List<YarnMatch> yarnMatches)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load<Rgb24>(quantizedImageData);
            
            // Resize image to exact stitch dimensions
            image.Mutate(x => x.Resize(targetWidth, targetHeight));
            
            var grid = new StitchGrid
            {
                Width = targetWidth,
                Height = targetHeight,
                Cells = new StitchCell[targetWidth, targetHeight]
            };
            
            // Build color-to-index lookup
            var colorLookup = new Dictionary<string, int>();
            for (int i = 0; i < yarnMatches.Count; i++)
            {
                var yarn = yarnMatches[i];
                var key = $"{yarn.R},{yarn.G},{yarn.B}";
                colorLookup[key] = i;
            }
            
            // Process each pixel/stitch
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        var pixel = row[x];
                        var key = $"{pixel.R},{pixel.G},{pixel.B}";
                        
                        // Find matching yarn
                        int yarnIndex = 0;
                        if (colorLookup.ContainsKey(key))
                        {
                            yarnIndex = colorLookup[key];
                        }
                        else
                        {
                            // Find closest match by LAB distance
                            var (l, a, b) = ColorConverter.RgbToLab(pixel.R, pixel.G, pixel.B);
                            yarnIndex = FindClosestYarnMatch(l, a, b, yarnMatches);
                        }
                        
                        var symbol = yarnIndex < _symbols.Length 
                            ? _symbols[yarnIndex] 
                            : (yarnIndex + 1).ToString();
                        
                        grid.Cells[x, y] = new StitchCell
                        {
                            YarnMatchIndex = yarnIndex,
                            Symbol = symbol,
                            HexColor = yarnMatches[yarnIndex].HexColor
                        };
                    }
                }
            });
            
            return grid;
        });
    }
    
    private int FindClosestYarnMatch(double l, double a, double b, List<YarnMatch> yarnMatches)
    {
        int bestIndex = 0;
        double bestDistance = double.MaxValue;
        
        for (int i = 0; i < yarnMatches.Count; i++)
        {
            var yarn = yarnMatches[i];
            var distance = ColorConverter.CalculateLabDistance(
                l, a, b,
                yarn.Lab_L, yarn.Lab_A, yarn.Lab_B);
            
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        
        return bestIndex;
    }
}
```

### Step 49: Register Grid Service

Add to `Program.cs`:

```csharp
builder.Services.AddScoped<IGridGenerationService, GridGenerationService>();
```

### Step 50: Update PatternController to Generate Grid

Update `DownloadPdf` action in `PatternController.cs`:

```csharp
// Add to constructor
private readonly IGridGenerationService _gridService;

public PatternController(
    StitchLensDbContext context,
    IImageProcessingService imageService,
    IColorQuantizationService colorService,
    IYarnMatchingService yarnMatchingService,
    IPdfGenerationService pdfService,
    IGridGenerationService gridService)
{
    _context = context;
    _imageService = imageService;
    _colorService = colorService;
    _yarnMatchingService = yarnMatchingService;
    _pdfService = pdfService;
    _gridService = gridService;
}

// Update DownloadPdf action
public async Task<IActionResult> DownloadPdf(int id)
{
    var project = await _context.Projects
        .Include(p => p.YarnBrand)
        .FirstOrDefaultAsync(p => p.Id == id);
    
    if (project == null)
        return NotFound();
    
    // Deserialize yarn matches
    var yarnMatches = new List<YarnMatch>();
    if (!string.IsNullOrEmpty(project.PaletteJson))
    {
        yarnMatches = System.Text.Json.JsonSerializer
            .Deserialize<List<YarnMatch>>(project.PaletteJson) ?? new List<YarnMatch>();
    }
    
    // Load quantized image
    byte[] imageData = Array.Empty<byte>();
    if (!string.IsNullOrEmpty(project.ProcessedImagePath) && 
        System.IO.File.Exists(project.ProcessedImagePath))
    {
        imageData = await System.IO.File.ReadAllBytesAsync(project.ProcessedImagePath);
    }
    
    // Generate stitch grid
    int stitchWidth = (int)(project.WidthInches * project.MeshCount);
    int stitchHeight = (int)(project.HeightInches * project.MeshCount);
    
    var stitchGrid = await _gridService.GenerateStitchGridAsync(
        imageData,
        stitchWidth,
        stitchHeight,
        yarnMatches);
    
    // Create PDF data
    var pdfData = new PatternPdfData
    {
        Title = project.Title,
        MeshCount = project.MeshCount,
        WidthInches = project.WidthInches,
        HeightInches = project.HeightInches,
        WidthStitches = stitchWidth,
        HeightStitches = stitchHeight,
        StitchType = project.StitchType,
        QuantizedImageData = imageData,
        YarnMatches = yarnMatches,
        YarnBrand = project.YarnBrand?.Name ?? "Unknown",
        StitchGrid = stitchGrid
    };
    
    // Generate PDF
    var pdfBytes = await _pdfService.GeneratePatternPdfAsync(pdfData);
    
    // Return as download
    var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
    return File(pdfBytes, "application/pdf", fileName);
}
```

### Step 51: Update PatternPdfData

Update the class in `IPdfGenerationService.cs`:

```csharp
public class PatternPdfData
{
    public string Title { get; set; } = "Needlepoint Pattern";
    public int MeshCount { get; set; }
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int WidthStitches { get; set; }
    public int HeightStitches { get; set; }
    public string StitchType { get; set; } = "Tent";
    public byte[] QuantizedImageData { get; set; } = Array.Empty<byte>();
    public List<YarnMatch> YarnMatches { get; set; } = new();
    public string YarnBrand { get; set; } = "DMC";
    public StitchGrid? StitchGrid { get; set; }  // Add this
}
```

### Step 52: Update PDF Service to Render Grid

Add this method to `PdfGenerationService.cs`:

```csharp
private void RenderStitchGridPages(IContainer container, PatternPdfData data)
{
    if (data.StitchGrid == null) return;
    
    // Determine grid page size (how many stitches fit per page)
    // At 10 stitches per inch, we can fit about 70 stitches width, 90 height on letter with margins
    const int stitchesPerPageWidth = 50;
    const int stitchesPerPageHeight = 65;
    
    int pagesWide = (int)Math.Ceiling((double)data.StitchGrid.Width / stitchesPerPageWidth);
    int pagesHigh = (int)Math.Ceiling((double)data.StitchGrid.Height / stitchesPerPageHeight);
    
    for (int pageY = 0; pageY < pagesHigh; pageY++)
    {
        for (int pageX = 0; pageX < pagesWide; pageX++)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape()); // Landscape for more width
                page.Margin(0.4f, Unit.Inch);
                page.PageColor(Colors.White);
                
                int startX = pageX * stitchesPerPageWidth;
                int startY = pageY * stitchesPerPageHeight;
                int endX = Math.Min(startX + stitchesPerPageWidth, data.StitchGrid.Width);
                int endY = Math.Min(startY + stitchesPerPageHeight, data.StitchGrid.Height);
                
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Stitch Chart - Section {pageX + 1},{pageY + 1}")
                            .FontSize(12).Bold();
                        row.RelativeItem().AlignRight()
                            .Text($"Rows {startY + 1}-{endY}, Columns {startX + 1}-{endX}")
                            .FontSize(10);
                    });
                    col.Item().PaddingTop(3).LineHorizontal(1);
                });
                
                page.Content().Table(table =>
                {
                    // Column for row numbers + one column per stitch
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(25); // Row number column
                        for (int x = startX; x < endX; x++)
                        {
                            columns.ConstantColumn(12); // Each stitch cell
                        }
                    });
                    
                    // Header row with column numbers
                    table.Header(header =>
                    {
                        header.Cell().Text("").FontSize(6); // Empty corner
                        for (int x = startX; x < endX; x++)
                        {
                            var colNum = (x + 1).ToString();
                            header.Cell().Padding(1).AlignCenter()
                                .Text(colNum).FontSize(5).Bold();
                        }
                    });
                    
                    // Grid rows
                    for (int y = startY; y < endY; y++)
                    {
                        // Row number
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(2)
                            .AlignCenter().Text((y + 1).ToString()).FontSize(6).Bold();
                        
                        // Stitch cells
                        for (int x = startX; x < endX; x++)
                        {
                            var cell = data.StitchGrid.Cells[x, y];
                            table.Cell()
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(1)
                                .AlignCenter().AlignMiddle()
                                .Text(cell.Symbol).FontSize(8);
                        }
                    }
                });
                
                page.Footer().AlignCenter().Text($"Page {pageX + 1 + (pageY * pagesWide)} of {pagesWide * pagesHigh}")
                    .FontSize(8);
            });
        }
    }
}
```

### Step 53: Call Grid Rendering in Main Document

Update the `GeneratePatternPdfAsync` method in `PdfGenerationService.cs` to call the grid rendering:

```csharp
public async Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data)
{
    return await Task.Run(() =>
    {
        var document = Document.Create(container =>
        {
            // First page: Overview with larger image
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0.5f, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));
                
                // ... existing header code ...
                
                page.Content().PaddingTop(10).Column(column =>
                {
                    // Make image larger - full page width
                    column.Item().Text("Pattern Preview").FontSize(12).Bold();
                    
                    if (data.QuantizedImageData != null && data.QuantizedImageData.Length > 0)
                    {
                        column.Item().PaddingTop(5).PaddingBottom(10)
                            .Height(4, Unit.Inch)  // Larger image
                            .AlignCenter()
                            .Image(data.QuantizedImageData, ImageScaling.FitArea);
                    }
                    
                    // ... rest of existing content ...
                });
                
                // ... existing footer ...
            });
            
            // Add stitch grid pages
            RenderStitchGridPages(container, data);
        });
        
        return document.GeneratePdf();
    });
}
```

### Step 54: Test the Grid!

```bash
dotnet watch run
```

Now when you download a PDF, you should get:
- **Page 1**: Large color preview image (4 inches)
- **Pages 2-3**: Shopping list
- **Page 4**: Instructions
- **Page 5**: Color legend
- **Pages 6+**: Gridded stitch charts with symbols!

The grid will show each stitch with its symbol, row/column numbers for navigation, and will automatically span multiple pages for larger patterns.

Try it out! The grid is the heart of a needlepoint pattern. 🧵📐  
