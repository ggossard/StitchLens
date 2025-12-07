# StitchLens: Complete Implementation Guide

## Project Overview

**StitchLens** is a web application that converts photos into professional needlepoint patterns with:
- Color quantization using K-means clustering in LAB color space
- Yarn matching to DMC colors using ΔE2000 algorithm
- PDF generation with gridded stitch charts
- Shopping lists with accurate yarn quantities

## Technology Stack

- **Backend:** ASP.NET Core 9 (MVC)
- **Frontend:** Razor views with Bootstrap 5 + Cropper.js
- **Database:** SQLite (development) - easily switchable to SQL Server
- **Image Processing:** ImageSharp
- **PDF Generation:** QuestPDF
- **Pattern:** MVC architecture with clean service layer separation

---

## Phase 1: Project Setup

### Step 1: Create the Project Structure

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

### Step 2: Install Required NuGet Packages

```bash
# In StitchLens.Web/
cd StitchLens.Web
dotnet add package SixLabors.ImageSharp
dotnet add package SixLabors.ImageSharp.Web
dotnet add package QuestPDF
dotnet add package Stripe.net
dotnet add package Microsoft.EntityFrameworkCore.Design

# In StitchLens.Data/
cd ../StitchLens.Data
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.EntityFrameworkCore.Design

# In StitchLens.Core/
cd ../StitchLens.Core
dotnet add package SixLabors.ImageSharp
dotnet add package QuestPDF
```

### Step 3: Install Entity Framework Tools

```bash
dotnet tool install --global dotnet-ef
# Or update if already installed:
dotnet tool update --global dotnet-ef
```

### Step 4: Create Domain Models

Create `StitchLens.Data/Models/User.cs`:

```csharp
namespace StitchLens.Data.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PlanType { get; set; } = "Free";
    public DateTime CreatedAt { get; set; }
    
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
```

Create `StitchLens.Data/Models/Project.cs`:

```csharp
namespace StitchLens.Data.Models;

public class Project
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = "Untitled Pattern";
    public string OriginalImagePath { get; set; } = string.Empty;
    public string? ProcessedImagePath { get; set; }
    
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent";
    
    public int? YarnBrandId { get; set; }
    public string? PaletteJson { get; set; }
    public string? PdfPath { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public User? User { get; set; }
    public YarnBrand? YarnBrand { get; set; }
}
```

Create `StitchLens.Data/Models/YarnBrand.cs`:

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

Create `StitchLens.Data/Models/YarnColor.cs`:

```csharp
namespace StitchLens.Data.Models;

public class YarnColor
{
    public int Id { get; set; }
    public int YarnBrandId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HexColor { get; set; } = string.Empty;
    
    public double Lab_L { get; set; }
    public double Lab_A { get; set; }
    public double Lab_B { get; set; }
    
    public int YardsPerSkein { get; set; } = 8;
    
    public YarnBrand YarnBrand { get; set; } = null!;
}
```

### Step 5: Create Database Context

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
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
        });
        
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
        
        modelBuilder.Entity<YarnBrand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
        });
        
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

### Step 6: Configure Database Connection

Update `StitchLens.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=stitchlens.db"
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

### Step 7: Register Services in Program.cs

Update `StitchLens.Web/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StitchLens.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<StitchLensDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

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

### Step 8: Create Initial Migration

```bash
cd StitchLens.Web
dotnet ef migrations add InitialCreate --project ../StitchLens.Data --startup-project .
dotnet ef database update
```

---

## Phase 2: Image Upload & Processing

### Step 9: Create Image Processing Service Interface

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

### Step 10: Implement Image Processing Service

Create `StitchLens.Core/Services/ImageProcessingService.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using Image = SixLabors.ImageSharp.Image;

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
        
        // CRITICAL: Auto-orient based on EXIF first
        image.Mutate(x => x.AutoOrient());
        
        if (cropData != null)
        {
            var cropX = Math.Max(0, Math.Min(cropData.X, image.Width - 1));
            var cropY = Math.Max(0, Math.Min(cropData.Y, image.Height - 1));
            var cropWidth = Math.Min(cropData.Width, image.Width - cropX);
            var cropHeight = Math.Min(cropData.Height, image.Height - cropY);
            
            if (cropWidth > 0 && cropHeight > 0)
            {
                image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, cropWidth, cropHeight)));
            }
        }
        
        const int maxDimension = 2000;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            var ratio = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
            image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
        }
        
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

### Step 11: Create Pattern Controller

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
    
    public IActionResult Upload()
    {
        return View();
    }
    
    [HttpPost]
    [Route("Pattern/ProcessUpload")]
    public async Task<IActionResult> ProcessUpload(
        IFormFile imageFile,
        int cropX, int cropY, int cropWidth, int cropHeight,
        int originalWidth, int originalHeight)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            ModelState.AddModelError("", "Please select an image file.");
            return View("Upload");
        }
        
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
        {
            ModelState.AddModelError("", "Only JPG and PNG files are allowed.");
            return View("Upload");
        }
        
        if (cropX < 0 || cropY < 0 || cropWidth <= 0 || cropHeight <= 0)
        {
            ModelState.AddModelError("", "Invalid crop dimensions.");
            return View("Upload");
        }
        
        CropData? cropData = new CropData { X = cropX, Y = cropY, Width = cropWidth, Height = cropHeight };
        
        using var stream = imageFile.OpenReadStream();
        var processed = await _imageService.ProcessUploadAsync(stream, cropData);
        
        var fileName = $"{Guid.NewGuid()}.png";
        var filePath = await _imageService.SaveImageAsync(processed, fileName);
        
        var project = new Project
        {
            OriginalImagePath = filePath,
            CreatedAt = DateTime.UtcNow,
            WidthInches = processed.Width / 96m,
            HeightInches = processed.Height / 96m
        };
        
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        
        return RedirectToAction("Configure", new { id = project.Id });
    }
}
```

### Step 12: Create Upload View with Cropping

Create `StitchLens.Web/Views/Pattern/Upload.cshtml`:

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
                <div id="uploadSection">
                    <div class="card">
                        <div class="card-body">
                            <div class="mb-3">
                                <label for="imageFile" class="form-label">Choose an image</label>
                                <input type="file" class="form-control" id="imageFile" name="imageFile" 
                                       accept="image/jpeg,image/png,image/jpg" required>
                                <div class="form-text">JPG or PNG format, recommended size: 500-2000 pixels</div>
                            </div>
                        </div>
                    </div>
                </div>
                
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
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.reset()">Reset</button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.clear()">Clear</button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.setDragMode('move')">Move Image</button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary" onclick="cropper.setDragMode('crop')">Crop Area</button>
                                    </div>
                                    <hr>
                                    <h6 class="mt-3">Aspect Ratio</h6>
                                    <div class="d-grid gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(NaN)">Free</button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(1)">Square (1:1)</button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(4/3)">4:3</button>
                                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="cropper.setAspectRatio(16/9)">16:9</button>
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
                    
                    <input type="hidden" name="cropX" id="cropX">
                    <input type="hidden" name="cropY" id="cropY">
                    <input type="hidden" name="cropWidth" id="cropWidth">
                    <input type="hidden" name="cropHeight" id="cropHeight">
                    <input type="hidden" name="originalWidth" id="originalWidth">
                    <input type="hidden" name="originalHeight" id="originalHeight">
                </div>
                
                <div asp-validation-summary="All" class="text-danger mt-3"></div>
                
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
        let naturalWidth = 0;
        let naturalHeight = 0;
        
        document.getElementById('imageFile').addEventListener('change', function(e) {
            const file = e.target.files[0];
            if (!file) return;
            
            if (file.size > 10 * 1024 * 1024) {
                alert('File is too large. Please choose an image under 10MB.');
                this.value = '';
                return;
            }
            
            const reader = new FileReader();
            reader.onload = function(event) {
                const tempImg = new Image();
                tempImg.onload = function() {
                    naturalWidth = tempImg.naturalWidth;
                    naturalHeight = tempImg.naturalHeight;
                    
                    document.getElementById('uploadSection').style.display = 'none';
                    document.getElementById('cropSection').style.display = 'block';
                    document.getElementById('cancelCrop').style.display = 'block';
                    document.getElementById('continueBtn').style.display = 'block';
                    
                    const image = document.getElementById('cropImage');
                    image.src = event.target.result;
                    
                    if (cropper) cropper.destroy();
                    
                    cropper = new Cropper(image, {
                        viewMode: 1,
                        dragMode: 'move',
                        aspectRatio: NaN,
                        autoCropArea: 0.8,
                        ready: function() {
                            document.getElementById('originalWidth').value = naturalWidth;
                            document.getElementById('originalHeight').value = naturalHeight;
                        },
                        crop: function(event) {
                            const data = cropper.getData();
                            const imageData = cropper.getImageData();
                            const scaleX = naturalWidth / imageData.width;
                            const scaleY = naturalHeight / imageData.height;
                            
                            const cropX = Math.max(0, Math.round(data.x * scaleX));
                            const cropY = Math.max(0, Math.round(data.y * scaleY));
                            const cropWidth = Math.round(data.width * scaleX);
                            const cropHeight = Math.round(data.height * scaleY);
                            
                            document.getElementById('cropInfo').innerHTML = `Crop: ${cropWidth} × ${cropHeight}px`;
                            document.getElementById('cropX').value = cropX;
                            document.getElementById('cropY').value = cropY;
                            document.getElementById('cropWidth').value = cropWidth;
                            document.getElementById('cropHeight').value = cropHeight;
                        }
                    });
                };
                tempImg.src = event.target.result;
            };
            reader.readAsDataURL(file);
        });
        
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
    </script>
}
```

### Step 13: Add Cropper.js to Layout

Update `Views/Shared/_Layout.cshtml` to include Cropper.js in the head:

```html
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.css" />
```

And before the closing body tag:

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.js"></script>
```

### Step 14: Register Image Service and Configure Static Files

Update `Program.cs`:

```csharp
// After AddDbContext, add:
var uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton<IImageProcessingService>(
    new ImageProcessingService(uploadPath));

// After app.UseStaticFiles(), add:
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});
```

---

## Phase 3: Configuration UI

### Step 15: Create Configure ViewModel

Create `StitchLens.Web/Models/ConfigureViewModel.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StitchLens.Web.Models;

public class ConfigureViewModel
{
    public int ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    
    public int MeshCount { get; set; } = 14;
    public decimal WidthInches { get; set; }
    public decimal HeightInches { get; set; }
    public int MaxColors { get; set; } = 40;
    public string StitchType { get; set; } = "Tent";
    public int? YarnBrandId { get; set; }
    public List<SelectListItem> YarnBrands { get; set; } = new();
    
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
    
    public int WidthStitches => (int)(WidthInches * MeshCount);
    public int HeightStitches => (int)(HeightInches * MeshCount);
}
```

### Step 16: Add Configure Actions to Controller

Add to `PatternController.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using StitchLens.Web.Models;

public async Task<IActionResult> Configure(int id)
{
    var project = await _context.Projects.FindAsync(id);
    if (project == null)
        return NotFound();
    
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
        WidthInches = Math.Round(project.WidthInches, 1),
        HeightInches = Math.Round(project.HeightInches, 1),
        MaxColors = project.MaxColors,
        StitchType = project.StitchType,
        YarnBrandId = project.YarnBrandId,
        YarnBrands = yarnBrands
    };
    
    return View(viewModel);
}
```

### Step 17: Create Configure View

Create `Views/Pattern/Configure.cshtml`:

```html
@model StitchLens.Web.Models.ConfigureViewModel
@{
    ViewData["Title"] = "Configure Pattern";
}

<div class="container mt-4">
    <h2>Configure Your Pattern</h2>
    <p class="lead">Set your canvas dimensions and color preferences</p>
    
    <div class="row">
        <div class="col-md-6">
            <div class="card">
                <div class="card-header"><h5>Your Image</h5></div>
                <div class="card-body text-center">
                    <img src="@Model.ImageUrl" alt="Uploaded image" class="img-fluid" style="max-height: 400px;">
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
        
        <div class="col-md-6">
            <form asp-action="Configure" method="post" id="