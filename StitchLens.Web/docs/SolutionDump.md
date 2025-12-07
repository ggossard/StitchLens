# StitchLens — Solution dump

This single-file dump captures the workspace structure and the key source files for the `StitchLens` solution (ASP.NET Core, `net9.0`). Files below are the most important project files, configuration, data models, services and the app entry point.

## Summary
- Solution targets: `net9.0`
- Web project: `StitchLens.Web` (ASP.NET Core MVC / Razor views)
- Core library: `StitchLens.Core`
- Data library: `StitchLens.Data`
- SQLite connection string configured in `StitchLens.Web/appsettings.json`
- Uploads stored under an `uploads` folder (static files served)

## Project tree (top-level)
- `StitchLens.sln`
- `StitchLens.Web/`
 - `Program.cs`
 - `appsettings.json`
 - `StitchLens.Web.csproj`
 - `uploads/` (static)
 - `SeedData/`
 - `Controllers/PatternController.cs` (pattern flows)
- `StitchLens.Core/`
 - `Services/ImageProcessingService.cs`
 - `Services/IImageProcessingService.cs`
 - `StitchLens.Core.csproj`
- `StitchLens.Data/`
 - `StitchLensDbContext.cs`
 - `DbInitializer.cs`
 - `Models/` (`User.cs`, `Project.cs`, `YarnBrand.cs`, `YarnColor.cs`)
 - `StitchLens.Data.csproj`

---

## Key files

### `StitchLens.Web/StitchLens.Web.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

 <ItemGroup>
 <Compile Include="docs\Claude Strategy discussion.md" />
 </ItemGroup>

 <ItemGroup>
 <ProjectReference Include="..\StitchLens.Core\StitchLens.Core.csproj" />
 <ProjectReference Include="..\StitchLens.Data\StitchLens.Data.csproj" />
 </ItemGroup>

 <ItemGroup>
 <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.10">
 <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
 <PrivateAssets>all</PrivateAssets>
 </PackageReference>
 <PackageReference Include="QuestPDF" Version="2025.7.3" />
 <PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />
 <PackageReference Include="SixLabors.ImageSharp.Web" Version="3.2.0" />
 <PackageReference Include="Stripe.net" Version="49.0.0" />
 </ItemGroup>

 <ItemGroup>
 <Folder Include="SeedData\" />
 </ItemGroup>

 <PropertyGroup>
 <TargetFramework>net9.0</TargetFramework>
 <Nullable>enable</Nullable>
 <ImplicitUsings>enable</ImplicitUsings>
 </PropertyGroup>

</Project>
```

### `StitchLens.Core/StitchLens.Core.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">

 <ItemGroup>
 <ProjectReference Include="..\StitchLens.Data\StitchLens.Data.csproj" />
 </ItemGroup>

 <ItemGroup>
 <PackageReference Include="QuestPDF" Version="2025.7.3" />
 <PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />
 </ItemGroup>

 <PropertyGroup>
 <TargetFramework>net9.0</TargetFramework>
 <ImplicitUsings>enable</ImplicitUsings>
 <Nullable>enable</Nullable>
 </PropertyGroup>

</Project>
```

### `StitchLens.Data/StitchLens.Data.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">

 <PropertyGroup>
 <TargetFramework>net9.0</TargetFramework>
 <ImplicitUsings>enable</ImplicitUsings>
 <Nullable>enable</Nullable>
 </PropertyGroup>

 <ItemGroup>
 <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.10">
 <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
 <PrivateAssets>all</PrivateAssets>
 </PackageReference>
 <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.10" />
 <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.10">
 <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
 <PrivateAssets>all</PrivateAssets>
 </PackageReference>
 </ItemGroup>

</Project>
```

### `StitchLens.Web/Program.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using StitchLens.Core.Services;
using StitchLens.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IColorQuantizationService, ColorQuantizationService>();

builder.Services.AddScoped<IYarnMatchingService, YarnMatchingService>();

builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();

builder.Services.AddScoped<IGridGenerationService, GridGenerationService>();

// Register database context
builder.Services.AddDbContext<StitchLensDbContext>(options =>
 options.UseSqlite(
 builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application services
var uploadPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton<IImageProcessingService>(
 new ImageProcessingService(uploadPath));

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope()) {
 var services = scope.ServiceProvider;
 var context = services.GetRequiredService<StitchLensDbContext>();
 var env = services.GetRequiredService<IWebHostEnvironment>();
 DbInitializer.Initialize(context, env.ContentRootPath);
}

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

// After app.UseStaticFiles(); add:
app.UseStaticFiles(new StaticFileOptions
{
 FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
 Path.Combine(builder.Environment.ContentRootPath, "uploads")),
 RequestPath = "/uploads"
});

app.MapControllerRoute(
 name: "default",
 pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### `StitchLens.Web/appsettings.json`
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

### `StitchLens.Web/Controllers/PatternController.cs`
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
 private readonly IColorQuantizationService _colorService;
 private readonly IYarnMatchingService _yarnMatchingService;
 private readonly IPdfGenerationService _pdfService;
 private readonly IGridGenerationService _grid_service;


 public PatternController(
 StitchLensDbContext context,
 IImageProcessingService imageService,
 IColorQuantizationService colorService,
 IYarnMatchingService yarnMatchingService,
 IPdfGenerationService pdfService,
 IGridGenerationService gridService) {
 _context = context;
 _image_service = imageService;
 _color_service = colorService;
 _yarnMatchingService = yarnMatchingService;
 _pdfService = pdfService;
 _grid_service = gridService;
 }

 // Step1: Show upload form
 public IActionResult Upload()
 {
 return View();
 }

 // Step2: Process uploaded image with crop data
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
 if (imageFile == null || imageFile.Length ==0)
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

 // Validate crop dimensions are within bounds
 if (cropX <0 || cropY <0 || cropWidth <=0 || cropHeight <=0)
 {
 ModelState.AddModelError("", "Invalid crop dimensions.");
 return View("Upload");
 }

 if (cropX + cropWidth > originalWidth || cropY + cropHeight > originalHeight)
 {
 ModelState.AddModelError("", "Crop area exceeds image bounds.");
 return View("Upload");
 }

 // Create crop data object
 CropData? cropData = null;
 if (cropWidth >0 && cropHeight >0)
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
 var processed = await _image_service.ProcessUploadAsync(stream, cropData);

 // Save to disk with unique filename
 var fileName = $"{Guid.NewGuid()}.png";
 var filePath = await _image_service.SaveImageAsync(processed, fileName);

 // Create project record
 var project = new Project
 {
 OriginalImagePath = filePath,
 CreatedAt = DateTime.UtcNow,
 WidthInches = processed.Width /96m,
 HeightInches = processed.Height /96m
 };

 _context.Projects.Add(project);
 await _context.SaveChangesAsync();

 // Redirect to configuration page
 return RedirectToAction("Configure", new { id = project.Id });
 }

 // Step3: Show settings form
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
 WidthInches = Math.Round(project.WidthInches,1), // Round to1 decimal place
 HeightInches = Math.Round(project.HeightInches,1), // Round to1 decimal place
 MaxColors = project.MaxColors,
 StitchType = project.StitchType,
 YarnBrandId = project.YarnBrandId,
 YarnBrands = yarnBrands
 };

 return View(viewModel);
 }

 // Step4: Process configuration and start pattern generation
 [HttpPost]
 public async Task<IActionResult> Configure(ConfigureViewModel model) {
 if (!ModelState.IsValid) {
 model.YarnBrands = await _context.YarnBrands
 .Where(b => b.IsActive)
 .OrderBy(b => b.Name)
 .Select(b => new SelectListItem {
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
 var quantized = await _color_service.QuantizeAsync(imageBytes, project.MaxColors);

 // Save quantized image
 var quantizedFileName = $"{Path.GetFileNameWithoutExtension(project.OriginalImagePath)}_quantized.png";
 var quantizedPath = Path.Combine(Path.GetDirectoryName(project.OriginalImagePath)!, quantizedFileName);
 await System.IO.File.WriteAllBytesAsync(quantizedPath, quantized.QuantizedImageData);
 project.ProcessedImagePath = quantizedPath;

 // Match colors to yarns if brand selected
 if (project.YarnBrandId.HasValue) {
 // Calculate total stitches
 int totalStitches = (int)(project.WidthInches * project.MeshCount *
 project.HeightInches * project.MeshCount);

 var yarnMatches = await _yarnMatching_service.MatchColorsToYarnAsync(
 quantized.Palette,
 project.YarnBrandId.Value,
 totalStitches); // Pass total stitches

 // Store matched yarn info as JSON
 project.PaletteJson = System.Text.Json.JsonSerializer.Serialize(yarnMatches);
 }
 else {
 // Store just the palette if no brand selected
 project.PaletteJson = System.Text.Json.JsonSerializer.Serialize(quantized.Palette);
 }

 await _context.SaveChangesAsync();

 return RedirectToAction("Preview", new { id = project.Id });
 }

 // Preview page
 public async Task<IActionResult> Preview(int id) {
 var project = await _context.Projects
 .Include(p => p.YarnBrand)
 .FirstOrDefaultAsync(p => p.Id == id);

 if (project == null)
 return NotFound();

 var viewModel = new PreviewViewModel {
 Project = project
 };

 // Deserialize palette data
 if (!string.IsNullOrEmpty(project.PaletteJson)) {
 if (project.YarnBrandId.HasValue) {
 // Has yarn matching
 viewModel.YarnMatches = System.Text.Json.JsonSerializer
 .Deserialize<List<YarnMatch>>(project.PaletteJson);
 }
 else {
 // No yarn matching - just palette
 viewModel.UnmatchedColors = System.Text.Json.JsonSerializer
 .Deserialize<List<ColorInfo>>(project.PaletteJson);
 }
 }

 return View(viewModel);
 }

 // Action for PDF download
 public async Task<IActionResult> DownloadPdf(int id) {
 var project = await _context.Projects
 .Include(p => p.YarnBrand)
 .FirstOrDefaultAsync(p => p.Id == id);

 if (project == null)
 return NotFound();

 // Deserialize yarn matches
 var yarnMatches = new List<YarnMatch>();
 if (!string.IsNullOrEmpty(project.PaletteJson)) {
 yarnMatches = System.Text.Json.JsonSerializer
 .Deserialize<List<YarnMatch>>(project.PaletteJson) ?? new List<YarnMatch>();
 }

 // Load quantized image
 byte[] imageData = Array.Empty<byte>();
 if (!string.IsNullOrEmpty(project.ProcessedImagePath) &&
 System.IO.File.Exists(project.ProcessedImagePath)) {
 imageData = await System.IO.File.ReadAllBytesAsync(project.ProcessedImagePath);
 }

 // Generate stitch grid
 int stitchWidth = (int)(project.WidthInches * project.MeshCount);
 int stitchHeight = (int)(project.HeightInches * project.MeshCount);

 var stitchGrid = await _grid_service.GenerateStitchGridAsync(
 imageData,
 stitchWidth,
 stitchHeight,
 yarnMatches);

 // Create PDF data
 var pdfData = new PatternPdfData {
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
 StitchGrid = stitchGrid,
 UseColoredGrid = true // Set to true to see colored grid, false for symbols only
 };

 // Generate PDF
 var pdfBytes = await _pdf_service.GeneratePatternPdfAsync(pdfData);

 // Return as download
 var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
 return File(pdfBytes, "application/pdf", fileName);
 }
}
```

---
(remaining files omitted for brevity; full dump contains data models, services, color science, migrations and PDF/grid generation code)
