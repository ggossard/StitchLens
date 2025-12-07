using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Models;

namespace StitchLens.Web.Controllers;

public class PatternController : Controller {
    private readonly StitchLensDbContext _context;
    private readonly IImageProcessingService _imageService;
    private readonly IColorQuantizationService _colorService;
    private readonly IYarnMatchingService _yarnMatchingService;
    private readonly IPdfGenerationService _pdfService;
    private readonly IGridGenerationService _gridService;
    private readonly UserManager<User> _userManager;

    public PatternController(
        StitchLensDbContext context,
        IImageProcessingService imageService,
        IColorQuantizationService colorService,
        IYarnMatchingService yarnMatchingService,
        IPdfGenerationService pdfService,
        IGridGenerationService gridService,
        UserManager<User> userManager) {
        _context = context;
        _imageService = imageService;
        _colorService = colorService;
        _yarnMatchingService = yarnMatchingService;
        _pdfService = pdfService;
        _gridService = gridService;
        _userManager = userManager;
    }

    // Step 1: Show upload form
    public IActionResult Upload() {
        return View();
    }

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
        int originalHeight) {
        if (imageFile == null || imageFile.Length == 0) {
            ModelState.AddModelError("", "Please select an image file.");
            return View("Upload");
        }

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower())) {
            ModelState.AddModelError("", "Only JPG and PNG files are allowed.");
            return View("Upload");
        }

        // Validate crop dimensions are within bounds
        if (cropX < 0 || cropY < 0 || cropWidth <= 0 || cropHeight <= 0) {
            ModelState.AddModelError("", "Invalid crop dimensions.");
            return View("Upload");
        }

        if (cropX + cropWidth > originalWidth || cropY + cropHeight > originalHeight) {
            ModelState.AddModelError("", "Crop area exceeds image bounds.");
            return View("Upload");
        }

        // Create crop data object
        CropData? cropData = null;
        if (cropWidth > 0 && cropHeight > 0) {
            cropData = new CropData {
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

        // Get current user ID if logged in
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true) {
            var userIdString = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userIdString)) {
                userId = int.Parse(userIdString);
            }
        }

        // Create project record
        var project = new Project {
            UserId = userId,  // Will be null for guest users
            OriginalImagePath = filePath,
            CreatedAt = DateTime.UtcNow,
            WidthInches = processed.Width / 96m,
            HeightInches = processed.Height / 96m
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        // Redirect to configuration page
        return RedirectToAction("Configure", new { id = project.Id });
    }

    // Step 3: Show settings form
    public async Task<IActionResult> Configure(int id) {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();

        // Get available yarn brands
        var yarnBrands = await _context.YarnBrands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToListAsync();

        var viewModel = new ConfigureViewModel {
            ProjectId = project.Id,
            ImageUrl = $"/uploads/{Path.GetFileName(project.OriginalImagePath)}",
            Title = project.Title,
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

    // Step 4: Process configuration and start pattern generation
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
        project.Title = model.Title;
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
        if (project.YarnBrandId.HasValue) {
            // Calculate total stitches
            int totalStitches = (int)(project.WidthInches * project.MeshCount *
                                     project.HeightInches * project.MeshCount);

            var yarnMatches = await _yarnMatchingService.MatchColorsToYarnAsync(
                quantized.Palette,
                project.YarnBrandId.Value,
                totalStitches);

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

        var stitchGrid = await _gridService.GenerateStitchGridAsync(
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
            UseColoredGrid = true
        };

        // Generate PDF
        var pdfBytes = await _pdfService.GeneratePatternPdfAsync(pdfData);

        // Return as download
        var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}