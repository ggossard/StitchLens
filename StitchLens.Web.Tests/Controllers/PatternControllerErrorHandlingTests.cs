using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StitchLens.Core.Services;
using StitchLens.Data;
using StitchLens.Data.Models;
using StitchLens.Web.Controllers;
using StitchLens.Web.Models;
using StitchLens.Web.Services;

namespace StitchLens.Web.Tests.Controllers;

[Trait("Category", "LaunchCritical")]
public class PatternControllerErrorHandlingTests {
    [Fact]
    public async Task ProcessUpload_ReturnsUploadViewWithFriendlyError_WhenImageProcessingFails() {
        using var db = CreateDb();
        var imageService = new Mock<IImageProcessingService>();
        imageService
            .Setup(s => s.ProcessUploadAsync(It.IsAny<Stream>(), It.IsAny<CropData?>()))
            .ThrowsAsync(new InvalidOperationException("Processing failed"));

        var controller = CreateController(db.Context, imageService: imageService.Object);

        var fileStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        IFormFile formFile = new FormFile(fileStream, 0, fileStream.Length, "imageFile", "sample.png") {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await controller.ProcessUpload(
            formFile,
            cropX: 0,
            cropY: 0,
            cropWidth: 10,
            cropHeight: 10,
            cropShape: "Rectangle",
            originalWidth: 100,
            originalHeight: 100);

        result.Should().BeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        viewResult.ViewName.Should().Be("Upload");
        controller.ModelState.ErrorCount.Should().BeGreaterThan(0);
        controller.ModelState[string.Empty]!.Errors.Any(e => e.ErrorMessage.Contains("couldn't process that image", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigurePost_RedirectsWithFriendlyMessage_WhenGenerationFails() {
        using var db = CreateDb();

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"stitchlens-test-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tempFilePath, new byte[] { 1, 2, 3, 4, 5 });

        try {
            var project = new Project {
                Id = 501,
                UserId = null,
                OriginalImagePath = tempFilePath,
                CreatedAt = DateTime.UtcNow,
                WidthInches = 5,
                HeightInches = 5,
                MeshCount = 13,
                MaxColors = 12,
                StitchType = "Tent"
            };
            db.Context.Projects.Add(project);
            await db.Context.SaveChangesAsync();

            var colorService = new Mock<IColorQuantizationService>();
            colorService
                .Setup(s => s.QuantizeAsync(It.IsAny<byte[]>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Quantization failed"));

            var controller = CreateController(db.Context, colorService: colorService.Object);

            var model = new ConfigureViewModel {
                ProjectId = project.Id,
                Title = "Test",
                CraftType = CraftType.Needlepoint,
                MeshCount = 13,
                WidthInches = 5,
                HeightInches = 5,
                MaxColors = 12,
                StitchType = "Tent",
                YarnBrands = new List<SelectListItem>(),
                AllYarnBrands = new List<YarnBrandOption>()
            };

            var result = await controller.Configure(model);

            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("Configure");
            controller.TempData["ErrorMessage"].Should().Be("We couldn't generate your pattern right now. Please try again.");
        }
        finally {
            if (File.Exists(tempFilePath)) {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public async Task DownloadPdf_RedirectsWithFriendlyMessage_WhenPdfGenerationFails() {
        using var db = CreateDb();

        var user = new User {
            Id = 777,
            UserName = "creator@example.com",
            Email = "creator@example.com",
            CurrentTier = SubscriptionTier.Creator
        };

        var project = new Project {
            Id = 778,
            UserId = user.Id,
            OriginalImagePath = "uploads/original.png",
            ProcessedImagePath = "uploads/missing.png",
            CreatedAt = DateTime.UtcNow,
            WidthInches = 4,
            HeightInches = 4,
            MeshCount = 13,
            PaletteJson = "[]"
        };

        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var gridService = new Mock<IGridGenerationService>();
        gridService
            .Setup(s => s.GenerateStitchGridAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<List<YarnMatch>>()))
            .ThrowsAsync(new Exception("Grid generation failed"));

        var controller = CreateController(db.Context, gridService: gridService.Object, userId: user.Id);

        var result = await controller.DownloadPdf(project.Id);

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Preview");
        controller.TempData["ErrorMessage"].Should().Be("We couldn't generate your PDF right now. Please try again.");
    }

    private static PatternController CreateController(
        StitchLensDbContext context,
        IImageProcessingService? imageService = null,
        IColorQuantizationService? colorService = null,
        IYarnMatchingService? yarnMatchingService = null,
        IPdfGenerationService? pdfService = null,
        IGridGenerationService? gridService = null,
        int? userId = null) {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue) {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) },
                "test"));
        }

        var controller = new PatternController(
            context,
            imageService ?? Mock.Of<IImageProcessingService>(),
            colorService ?? Mock.Of<IColorQuantizationService>(),
            yarnMatchingService ?? Mock.Of<IYarnMatchingService>(),
            pdfService ?? Mock.Of<IPdfGenerationService>(),
            gridService ?? Mock.Of<IGridGenerationService>(),
            CreateUserManagerMock().Object,
            Mock.Of<ITierConfigurationService>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IStripeCheckoutSessionService>(),
            NullLogger<PatternController>.Instance) {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private static Mock<UserManager<User>> CreateUserManagerMock() {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static TestDb CreateDb() {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<StitchLensDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new StitchLensDbContext(options);
        context.Database.EnsureCreated();

        return new TestDb(context, connection);
    }

    private sealed class TestDb : IDisposable {
        public StitchLensDbContext Context { get; }
        private readonly SqliteConnection _connection;

        public TestDb(StitchLensDbContext context, SqliteConnection connection) {
            Context = context;
            _connection = connection;
        }

        public void Dispose() {
            Context.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider {
        public IDictionary<string, object> LoadTempData(HttpContext context) {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) {
        }
    }
}
