using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Models;
using System.Security.Claims;
using static StitchLens.Web.Models.ConfigureViewModel;

namespace StitchLens.Web.Controllers;

public class PatternController : Controller {
    private readonly StitchLensDbContext _context;
    private readonly IImageProcessingService _imageService;
    private readonly IColorQuantizationService _colorService;
    private readonly IYarnMatchingService _yarnMatchingService;
    private readonly IPdfGenerationService _pdfService;
    private readonly IGridGenerationService _gridService;
    private readonly UserManager<User> _userManager;
    private readonly ITierConfigurationService _tierConfigService;

    public PatternController(
        StitchLensDbContext context,
        IImageProcessingService imageService,
        IColorQuantizationService colorService,
        IYarnMatchingService yarnMatchingService,
        IPdfGenerationService pdfService,
        IGridGenerationService gridService,
        UserManager<User> userManager,
        ITierConfigurationService tierConfigService) {
        _context = context;
        _imageService = imageService;
        _colorService = colorService;
        _yarnMatchingService = yarnMatchingService;
        _pdfService = pdfService;
        _gridService = gridService;
        _userManager = userManager;
        _tierConfigService = tierConfigService;
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
            HeightInches = processed.Height / 96m,
            YarnBrandId = null
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

        // Get ALL yarn brands with their craft type for client-side filtering
        var allBrands = await _context.YarnBrands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new YarnBrandOption {
                Id = b.Id,
                Name = b.Name,
                CraftType = (int)b.CraftType
            })
            .ToListAsync();

        // Debug: Log what we found
        Console.WriteLine($"Found {allBrands.Count} yarn brands:");
        foreach (var brand in allBrands) {
            Console.WriteLine($"  - {brand.Name} (CraftType: {brand.CraftType})");
        }

        // Get brands for current craft type (for initial display)
        var currentCraftBrands = allBrands
            .Where(b => b.CraftType == (int)project.CraftType)
            .Select(b => new SelectListItem {
                Value = b.Id.ToString(),
                Text = b.Name
            })
            .ToList();

        var viewModel = new ConfigureViewModel {
            ProjectId = project.Id,
            ImageUrl = $"/uploads/{Path.GetFileName(project.OriginalImagePath)}",
            Title = project.Title,
            CraftType = project.CraftType,
            MeshCount = project.MeshCount,
            WidthInches = Math.Round(project.WidthInches, 1),
            HeightInches = Math.Round(project.HeightInches, 1),
            MaxColors = project.MaxColors,
            StitchType = project.StitchType,
            YarnBrandId = project.YarnBrandId,
            YarnBrands = currentCraftBrands,
            AllYarnBrands = allBrands  // CRITICAL: Set this
        };

        // Debug: Verify it's set
        Console.WriteLine($"ViewModel.AllYarnBrands count: {viewModel.AllYarnBrands.Count}");

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
        project.CraftType = model.CraftType;
        project.MeshCount = model.MeshCount;
        project.WidthInches = model.WidthInches;
        project.HeightInches = model.HeightInches;
        project.MaxColors = model.MaxColors;
        project.StitchType = model.StitchType;
        project.YarnBrandId = model.YarnBrandId;

        await _context.SaveChangesAsync();

        // Calculate actual stitch dimensions
        int stitchWidth = (int)(project.WidthInches * project.MeshCount);
        int stitchHeight = (int)(project.HeightInches * project.MeshCount);
        int totalStitches = stitchWidth * stitchHeight;

        Console.WriteLine($"Pattern dimensions: {stitchWidth} x {stitchHeight} = {totalStitches} stitches");

        // Generate quantized pattern
        var imageBytes = await System.IO.File.ReadAllBytesAsync(project.OriginalImagePath);
        var quantized = await _colorService.QuantizeAsync(imageBytes, project.MaxColors);

        // CALCULATE SCALING FACTOR
        // Original image might be 500x500 pixels, but pattern is only 70x106 stitches
        var originalImage = await Image.LoadAsync<Rgb24>(project.OriginalImagePath);
        int originalPixels = originalImage.Width * originalImage.Height;
        double scaleFactor = (double)totalStitches / originalPixels;

        Console.WriteLine($"Original image: {originalImage.Width}x{originalImage.Height} = {originalPixels} pixels");
        Console.WriteLine($"Scale factor: {scaleFactor:F6}");

        // Scale the pixel counts in the palette to match stitch counts
        foreach (var color in quantized.Palette) {
            int originalCount = color.PixelCount;
            color.PixelCount = (int)Math.Round(color.PixelCount * scaleFactor);
            Console.WriteLine($"Color: {color.R},{color.G},{color.B} - Original: {originalCount}, Scaled: {color.PixelCount}");
        }

        // Verify the total
        int paletteTotal = quantized.Palette.Sum(c => c.PixelCount);
        Console.WriteLine($"Palette total after scaling: {paletteTotal} (should be ~{totalStitches})");

        // Save quantized image
        var quantizedFileName = $"{Path.GetFileNameWithoutExtension(project.OriginalImagePath)}_quantized.png";
        var quantizedPath = Path.Combine(Path.GetDirectoryName(project.OriginalImagePath)!, quantizedFileName);
        await System.IO.File.WriteAllBytesAsync(quantizedPath, quantized.QuantizedImageData);
        project.ProcessedImagePath = quantizedPath;

        // Match colors to yarns if brand selected
        if (project.YarnBrandId.HasValue) {
            var yarnMatches = await _yarnMatchingService.MatchColorsToYarnAsync(
                quantized.Palette,
                project.YarnBrandId.Value,
                totalStitches,
                project.CraftType);

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

        // Get current user for quota check
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        User? user = null;
        if (!string.IsNullOrEmpty(userId)) {
            user = await _context.Users
                .Include(u => u.ActiveSubscription)
                .FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

            // Reset counter if new month
            if (user != null && (user.LastDownloadDate.Month != DateTime.UtcNow.Month ||
                user.LastDownloadDate.Year != DateTime.UtcNow.Year)) {
                user.DownloadsThisMonth = 0;
                user.LastDownloadDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        var viewModel = new PreviewViewModel {
            Project = project,
            CraftType = project.CraftType,
            DownloadsUsed = user?.DownloadsThisMonth ?? 0,
            CurrentTier = user?.CurrentTier ?? SubscriptionTier.Free,
            DownloadLimit = user?.ActiveSubscription?.DownloadQuota
                 ?? await _tierConfigService.GetDownloadQuotaAsync(user?.CurrentTier ?? SubscriptionTier.Free)
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
    public async Task<IActionResult> DownloadPdf(int id, bool useColor = true) {
        var project = await _context.Projects
            .Include(p => p.YarnBrand)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        // Get current user and check download quota
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) {
            // Not logged in - redirect to login
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("DownloadPdf", "Pattern", new { id }) });
        }

        var user = await _context.Users
            .Include(u => u.ActiveSubscription)
            .FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

        if (user == null)
            return NotFound();

        // Reset counter if new month
        if (user.LastDownloadDate.Month != DateTime.UtcNow.Month ||
            user.LastDownloadDate.Year != DateTime.UtcNow.Year) {
            user.DownloadsThisMonth = 0;
            user.LastDownloadDate = DateTime.UtcNow;
        }

        // Check quota based on tier
        int downloadLimit = await _tierConfigService.GetDownloadQuotaAsync(user.CurrentTier);

        if (user.DownloadsThisMonth >= downloadLimit) {
            // Exceeded quota - redirect to upgrade page
            TempData["ErrorMessage"] = user.CurrentTier == SubscriptionTier.Free
                ? "You've used your 1 free download. Upgrade to download more patterns!"
                : $"You've reached your monthly limit of {downloadLimit} downloads. Upgrade for more!";

            return RedirectToAction("Pricing", "Home");
        }

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
            CraftType = project.CraftType,
            MeshCount = project.MeshCount,
            WidthInches = project.WidthInches,
            HeightInches = project.HeightInches,
            WidthStitches = stitchWidth,  // Fixed typo
            HeightStitches = stitchHeight,
            StitchType = project.StitchType,
            QuantizedImageData = imageData,
            YarnMatches = yarnMatches,
            YarnBrand = project.YarnBrand?.Name ?? "Unknown",
            StitchGrid = stitchGrid,
            UseColoredGrid = useColor
        };

        // Generate PDF
        var pdfBytes = await _pdfService.GeneratePatternPdfAsync(pdfData);

        // Increment download counter AFTER successful generation
        user.DownloadsThisMonth++;
        user.LastDownloadDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Return as download
        var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}