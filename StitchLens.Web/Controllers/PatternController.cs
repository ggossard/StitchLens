using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Stripe;
using Stripe.Checkout;
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
    private readonly IConfiguration _configuration;

    public PatternController(
        StitchLensDbContext context,
        IImageProcessingService imageService,
        IColorQuantizationService colorService,
        IYarnMatchingService yarnMatchingService,
        IPdfGenerationService pdfService,
        IGridGenerationService gridService,
        UserManager<User> userManager,
        ITierConfigurationService tierConfigService,
        IConfiguration configuration) {
        _context = context;
        _imageService = imageService;
        _colorService = colorService;
        _yarnMatchingService = yarnMatchingService;
        _pdfService = pdfService;
        _gridService = gridService;
        _userManager = userManager;
        _tierConfigService = tierConfigService;
        _configuration = configuration;

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
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
        string? cropShape,
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

        var parsedShape = CropShape.Rectangle;
        if (!string.IsNullOrWhiteSpace(cropShape) &&
            Enum.TryParse<CropShape>(cropShape, ignoreCase: true, out var shape)) {
            parsedShape = shape;
        }

        // Create crop data object
        CropData? cropData = null;
        if (cropWidth > 0 && cropHeight > 0) {
            cropData = new CropData {
                X = cropX,
                Y = cropY,
                Width = cropWidth,
                Height = cropHeight,
                Shape = parsedShape
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
            Public = project.Public,
            Tags = project.Tags,
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

        // Invalidate cached PDFs when settings are changed.
        DeleteCachedPdfFiles(project);
        project.PdfPath = null;

        User? currentUser = null;
        if (User.Identity?.IsAuthenticated == true) {
            var currentUserId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(currentUserId)) {
                currentUser = await _context.Users
                    .Include(u => u.ActiveSubscription)
                    .FirstOrDefaultAsync(u => u.Id == int.Parse(currentUserId));

                if (currentUser != null &&
                    (currentUser.LastPatternCreationDate.Month != DateTime.UtcNow.Month ||
                    currentUser.LastPatternCreationDate.Year != DateTime.UtcNow.Year)) {
                    currentUser.PatternsCreatedThisMonth = 0;
                    currentUser.LastPatternCreationDate = DateTime.UtcNow;
                }

                if (currentUser != null) {
                    if (currentUser.CurrentTier != SubscriptionTier.PayAsYouGo) {
                        int patternCreationQuota = currentUser.ActiveSubscription?.PatternCreationQuota
                            ?? await _tierConfigService.GetPatternCreationQuotaAsync(currentUser.CurrentTier);

                        if (currentUser.PatternsCreatedThisMonth >= patternCreationQuota) {
                            TempData["ErrorMessage"] = $"You've reached your monthly limit of {patternCreationQuota} patterns. Upgrade for more!";
                            return RedirectToAction("Pricing", "Home");
                        }
                    }
                }
            }
        }

        // Update project with settings
        project.Title = model.Title;
        project.Public = model.Public;
        project.Tags = NormalizeProjectTags(model.Tags);
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

        if (currentUser != null) {
            currentUser.PatternsCreatedThisMonth++;
            currentUser.LastPatternCreationDate = DateTime.UtcNow;

            if (currentUser.LastPatternDate.Date != DateTime.UtcNow.Date) {
                currentUser.PatternsCreatedToday = 1;
            }
            else {
                currentUser.PatternsCreatedToday++;
            }
            currentUser.LastPatternDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

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
            if (user != null && (user.LastPatternCreationDate.Month != DateTime.UtcNow.Month ||
                user.LastPatternCreationDate.Year != DateTime.UtcNow.Year)) {
                user.PatternsCreatedThisMonth = 0;
                user.LastPatternCreationDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        var viewModel = new PreviewViewModel {
            Project = project,
            CraftType = project.CraftType,
            PatternsCreatedThisMonth = user?.PatternsCreatedThisMonth ?? 0,
            CurrentTier = user?.CurrentTier ?? SubscriptionTier.PayAsYouGo,
            HasPaidForPattern = true,
            PatternCreationQuota = user?.ActiveSubscription?.PatternCreationQuota
                 ?? await _tierConfigService.GetPatternCreationQuotaAsync(user?.CurrentTier ?? SubscriptionTier.PayAsYouGo)
        };

        if (user != null && user.CurrentTier == SubscriptionTier.PayAsYouGo) {
            viewModel.HasPaidForPattern = await HasSuccessfulPatternPaymentAsync(user.Id, project.Id);
        }

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

        // Require login for PDF downloads
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) {
            // Not logged in - redirect to login
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("DownloadPdf", "Pattern", new { id, useColor }) });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

        if (user == null) {
            return RedirectToAction("Login", "Account");
        }

        if (user.CurrentTier == SubscriptionTier.PayAsYouGo) {
            bool alreadyPaidForPattern = await HasSuccessfulPatternPaymentAsync(user.Id, project.Id);

            if (!alreadyPaidForPattern) {
                return RedirectToAction("StartPatternPurchase", new { id, useColor });
            }
        }

        var cachedPdfPath = GetCachedPdfPath(project, useColor);
        if (System.IO.File.Exists(cachedPdfPath)) {
            project.Downloads++;

            if (useColor) {
                project.PdfPath = cachedPdfPath;
            }

            await _context.SaveChangesAsync();

            var cachedPdfBytes = await System.IO.File.ReadAllBytesAsync(cachedPdfPath);
            var cachedFileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(cachedPdfBytes, "application/pdf", cachedFileName);
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

        // Cache PDF on disk for subsequent downloads
        var cachedDirectory = Path.GetDirectoryName(cachedPdfPath);
        if (!string.IsNullOrEmpty(cachedDirectory) && !Directory.Exists(cachedDirectory)) {
            Directory.CreateDirectory(cachedDirectory);
        }
        await System.IO.File.WriteAllBytesAsync(cachedPdfPath, pdfBytes);

        if (useColor) {
            project.PdfPath = cachedPdfPath;
        }

        // Track successful PDF download count per project
        project.Downloads++;
        await _context.SaveChangesAsync();

        // Return as download
        var fileName = $"StitchLens_Pattern_{project.Id}_{DateTime.Now:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> StartPatternPurchase(int id, bool useColor = true) {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("StartPatternPurchase", "Pattern", new { id, useColor }) });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(userId));
        if (user == null) {
            return RedirectToAction("Login", "Account");
        }

        if (user.CurrentTier != SubscriptionTier.PayAsYouGo) {
            return RedirectToAction("DownloadPdf", new { id, useColor });
        }

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id);
        if (project == null) {
            TempData["ErrorMessage"] = "Project not found.";
            return RedirectToAction("Preview", new { id });
        }

        if (await HasSuccessfulPatternPaymentAsync(user.Id, project.Id)) {
            return RedirectToAction("DownloadPdf", new { id, useColor });
        }

        var tierConfig = await _tierConfigService.GetConfigAsync(SubscriptionTier.PayAsYouGo);
        var priceId = tierConfig.StripePerPatternPriceId ?? _configuration["Stripe:PriceIds:PerPattern"];

        if (string.IsNullOrWhiteSpace(priceId)) {
            TempData["ErrorMessage"] = "Pattern purchase is not configured yet. Please contact support.";
            return RedirectToAction("Preview", new { id });
        }

        try {
            var options = new SessionCreateOptions {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                CustomerEmail = user.Email,
                ClientReferenceId = user.Id.ToString(),
                LineItems = new List<SessionLineItemOptions> {
                    new SessionLineItemOptions {
                        Price = priceId,
                        Quantity = 1
                    }
                },
                SuccessUrl = Url.Action("CompletePatternPurchase", "Pattern", new { id, useColor }, Request.Scheme) + "&session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = Url.Action("PatternPurchaseCanceled", "Pattern", new { id }, Request.Scheme),
                Metadata = new Dictionary<string, string> {
                    { "user_id", user.Id.ToString() },
                    { "project_id", project.Id.ToString() },
                    { "purchase_type", "one_time_pattern" },
                    { "use_color", useColor.ToString() }
                }
            };

            if (!string.IsNullOrEmpty(user.StripeCustomerId)) {
                options.Customer = user.StripeCustomerId;
                options.CustomerEmail = null;
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Redirect(session.Url);
        }
        catch (StripeException ex) {
            TempData["ErrorMessage"] = $"Payment system error: {ex.Message}";
            return RedirectToAction("Preview", new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CompletePatternPurchase(int id, bool useColor = true, string? session_id = null) {
        if (string.IsNullOrWhiteSpace(session_id)) {
            TempData["ErrorMessage"] = "Invalid payment session.";
            return RedirectToAction("Preview", new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("CompletePatternPurchase", "Pattern", new { id, useColor, session_id }) });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(userId));
        if (user == null) {
            return RedirectToAction("Login", "Account");
        }

        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id);
        if (project == null) {
            TempData["ErrorMessage"] = "Project not found.";
            return RedirectToAction("Preview", new { id });
        }

        try {
            var service = new SessionService();
            var session = await service.GetAsync(session_id);

            if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)) {
                TempData["ErrorMessage"] = "Payment was not completed. Please try again.";
                return RedirectToAction("Preview", new { id });
            }

            if (!session.Metadata.TryGetValue("purchase_type", out var purchaseType) ||
                !string.Equals(purchaseType, "one_time_pattern", StringComparison.OrdinalIgnoreCase)) {
                TempData["ErrorMessage"] = "Invalid purchase session.";
                return RedirectToAction("Preview", new { id });
            }

            if (!session.Metadata.TryGetValue("user_id", out var metadataUserId) ||
                metadataUserId != user.Id.ToString() ||
                !session.Metadata.TryGetValue("project_id", out var metadataProjectId) ||
                metadataProjectId != project.Id.ToString()) {
                TempData["ErrorMessage"] = "Payment session does not match this project.";
                return RedirectToAction("Preview", new { id });
            }

            string? paymentIntentId = session.PaymentIntent?.Id;
            bool alreadyRecorded = !string.IsNullOrWhiteSpace(paymentIntentId)
                && await _context.PaymentHistory.AnyAsync(p => p.StripePaymentIntentId == paymentIntentId);

            if (!alreadyRecorded && !await HasSuccessfulPatternPaymentAsync(user.Id, project.Id)) {
                var payment = new PaymentHistory {
                    UserId = user.Id,
                    ProjectId = project.Id,
                    Type = PaymentType.OneTimePattern,
                    Amount = (session.AmountTotal ?? 0) / 100m,
                    Currency = session.Currency?.ToUpper() ?? "USD",
                    Status = PaymentStatus.Succeeded,
                    Description = $"One-time pattern purchase for project {project.Id}",
                    StripePaymentIntentId = paymentIntentId,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow
                };

                _context.PaymentHistory.Add(payment);

                if (string.IsNullOrWhiteSpace(user.StripeCustomerId) && !string.IsNullOrWhiteSpace(session.CustomerId)) {
                    user.StripeCustomerId = session.CustomerId;
                }

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Payment successful! Your pattern is unlocked. Click Download Your Pattern to get the PDF.";

            return RedirectToAction("Preview", new { id });
        }
        catch (StripeException ex) {
            TempData["ErrorMessage"] = $"Error verifying payment: {ex.Message}";
            return RedirectToAction("Preview", new { id });
        }
    }

    [HttpGet]
    public IActionResult PatternPurchaseCanceled(int id) {
        TempData["WarningMessage"] = "Pattern purchase was canceled. You can try again anytime.";
        return RedirectToAction("Preview", new { id });
    }

    private static string? NormalizeProjectTags(string? tags) {
        if (string.IsNullOrWhiteSpace(tags)) {
            return null;
        }

        var normalized = tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToList();

        return normalized.Count == 0 ? null : string.Join(',', normalized);
    }

    private async Task<bool> HasSuccessfulPatternPaymentAsync(int userId, int projectId) {
        return await _context.PaymentHistory.AnyAsync(p =>
            p.UserId == userId &&
            p.ProjectId == projectId &&
            p.Type == PaymentType.OneTimePattern &&
            p.Status == PaymentStatus.Succeeded);
    }

    private static string GetCachedPdfPath(Project project, bool useColor) {
        var requestedSuffix = useColor ? "_color" : "_bw";

        if (!string.IsNullOrWhiteSpace(project.PdfPath)) {
            var currentPath = project.PdfPath;
            var extension = Path.GetExtension(currentPath);

            if (currentPath.EndsWith("_color.pdf", StringComparison.OrdinalIgnoreCase) ||
                currentPath.EndsWith("_bw.pdf", StringComparison.OrdinalIgnoreCase)) {
                var currentSuffix = currentPath.EndsWith("_color.pdf", StringComparison.OrdinalIgnoreCase)
                    ? "_color"
                    : "_bw";

                if (currentSuffix == requestedSuffix) {
                    return currentPath;
                }

                return currentPath[..^($"{currentSuffix}.pdf".Length)] + $"{requestedSuffix}.pdf";
            }

            if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)) {
                return Path.ChangeExtension(currentPath, null) + $"{requestedSuffix}.pdf";
            }
        }

        var sourceDirectory = Path.GetDirectoryName(project.ProcessedImagePath)
            ?? Path.GetDirectoryName(project.OriginalImagePath)
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

        var fileName = $"pattern_{project.Id}{requestedSuffix}.pdf";
        return Path.Combine(sourceDirectory, fileName);
    }

    private static void DeleteCachedPdfFiles(Project project) {
        if (string.IsNullOrWhiteSpace(project.PdfPath)) {
            return;
        }

        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            project.PdfPath,
            GetCachedPdfPath(project, true),
            GetCachedPdfPath(project, false)
        };

        foreach (var path in knownPaths) {
            if (System.IO.File.Exists(path)) {
                System.IO.File.Delete(path);
            }
        }
    }
}
