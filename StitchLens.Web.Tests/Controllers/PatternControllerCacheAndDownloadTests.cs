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
public class PatternControllerCacheAndDownloadTests {
    [Fact]
    public async Task DownloadPdf_ReturnsCachedPdf_WhenCacheExists() {
        using var db = CreateDb();
        var tempDir = CreateTempDir();

        try {
            var user = new User { Id = 901, UserName = "creator@example.com", Email = "creator@example.com", CurrentTier = SubscriptionTier.Creator };
            var cachedPath = Path.Combine(tempDir, "pattern_300_color.pdf");
            var expectedBytes = new byte[] { 10, 20, 30 };
            await File.WriteAllBytesAsync(cachedPath, expectedBytes);

            var project = new Project {
                Id = 300,
                UserId = user.Id,
                OriginalImagePath = Path.Combine(tempDir, "original.png"),
                ProcessedImagePath = Path.Combine(tempDir, "processed.png"),
                PdfPath = cachedPath,
                Downloads = 0,
                CreatedAt = DateTime.UtcNow,
                WidthInches = 4,
                HeightInches = 4,
                MeshCount = 13,
                StitchType = "Tent",
                PaletteJson = "[]"
            };

            db.Context.Users.Add(user);
            db.Context.Projects.Add(project);
            await db.Context.SaveChangesAsync();

            var controller = CreateController(db.Context, user.Id);
            var result = await controller.DownloadPdf(project.Id, useColor: true);

            result.Should().BeOfType<FileContentResult>();
            var file = (FileContentResult)result;
            file.FileContents.Should().Equal(expectedBytes);

            var refreshed = await db.Context.Projects.FindAsync(project.Id);
            refreshed!.Downloads.Should().Be(1);
        }
        finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadPdf_RedirectsToPurchase_ForPayAsYouGoWithoutPayment() {
        using var db = CreateDb();
        var user = new User { Id = 902, UserName = "paygo@example.com", Email = "paygo@example.com", CurrentTier = SubscriptionTier.PayAsYouGo };
        var project = new Project {
            Id = 301,
            UserId = user.Id,
            OriginalImagePath = "uploads/original.png",
            ProcessedImagePath = "uploads/processed.png",
            CreatedAt = DateTime.UtcNow,
            WidthInches = 4,
            HeightInches = 4,
            MeshCount = 13,
            StitchType = "Tent",
            PaletteJson = "[]"
        };

        db.Context.Users.Add(user);
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();

        var controller = CreateController(db.Context, user.Id);
        var result = await controller.DownloadPdf(project.Id, useColor: true);

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("StartPatternPurchase");
    }

    [Fact]
    public async Task DownloadPdf_AllowsPayAsYouGoWithRecordedPayment() {
        using var db = CreateDb();
        var tempDir = CreateTempDir();

        try {
            var user = new User { Id = 903, UserName = "paygopaid@example.com", Email = "paygopaid@example.com", CurrentTier = SubscriptionTier.PayAsYouGo };
            var cachedPath = Path.Combine(tempDir, "pattern_302_color.pdf");
            var expectedBytes = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(cachedPath, expectedBytes);

            var project = new Project {
                Id = 302,
                UserId = user.Id,
                OriginalImagePath = Path.Combine(tempDir, "original.png"),
                ProcessedImagePath = Path.Combine(tempDir, "processed.png"),
                PdfPath = cachedPath,
                CreatedAt = DateTime.UtcNow,
                WidthInches = 4,
                HeightInches = 4,
                MeshCount = 13,
                StitchType = "Tent",
                PaletteJson = "[]"
            };

            var payment = new PaymentHistory {
                UserId = user.Id,
                ProjectId = project.Id,
                Type = PaymentType.OneTimePattern,
                Status = PaymentStatus.Succeeded,
                Amount = 5.95m,
                Currency = "USD"
            };

            db.Context.Users.Add(user);
            db.Context.Projects.Add(project);
            db.Context.PaymentHistory.Add(payment);
            await db.Context.SaveChangesAsync();

            var controller = CreateController(db.Context, user.Id);
            var result = await controller.DownloadPdf(project.Id, useColor: true);

            result.Should().BeOfType<FileContentResult>();
            ((FileContentResult)result).FileContents.Should().Equal(expectedBytes);
        }
        finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Configure_DeletesCachedPdfColorAndBw_OnSettingsChange() {
        using var db = CreateDb();
        var tempDir = CreateTempDir();

        try {
            var colorPath = Path.Combine(tempDir, "pattern_303_color.pdf");
            var bwPath = Path.Combine(tempDir, "pattern_303_bw.pdf");
            await File.WriteAllBytesAsync(colorPath, new byte[] { 1 });
            await File.WriteAllBytesAsync(bwPath, new byte[] { 2 });

            var originalPath = Path.Combine(tempDir, "original.png");
            await File.WriteAllBytesAsync(originalPath, new byte[] { 3, 4, 5 });

            var project = new Project {
                Id = 303,
                OriginalImagePath = originalPath,
                ProcessedImagePath = originalPath,
                PdfPath = colorPath,
                CreatedAt = DateTime.UtcNow,
                WidthInches = 4,
                HeightInches = 4,
                MeshCount = 13,
                MaxColors = 10,
                StitchType = "Tent",
                PaletteJson = "[]",
                CraftType = CraftType.Needlepoint
            };

            db.Context.Projects.Add(project);
            await db.Context.SaveChangesAsync();

            var colorService = new Mock<IColorQuantizationService>();
            colorService
                .Setup(s => s.QuantizeAsync(It.IsAny<byte[]>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Stop after invalidation"));

            var controller = CreateController(db.Context, userId: null, colorService: colorService.Object);

            var model = new ConfigureViewModel {
                ProjectId = project.Id,
                Title = "Updated",
                CraftType = CraftType.Needlepoint,
                MeshCount = 13,
                WidthInches = 4,
                HeightInches = 4,
                MaxColors = 10,
                StitchType = "Tent",
                YarnBrands = new List<SelectListItem>(),
                AllYarnBrands = new List<YarnBrandOption>()
            };

            await controller.Configure(model);

            File.Exists(colorPath).Should().BeFalse();
            File.Exists(bwPath).Should().BeFalse();

            var refreshed = await db.Context.Projects.FindAsync(project.Id);
            refreshed!.PdfPath.Should().BeNull();
        }
        finally {
            Directory.Delete(tempDir, true);
        }
    }

    private static PatternController CreateController(
        StitchLensDbContext context,
        int? userId,
        IColorQuantizationService? colorService = null) {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue) {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) },
                "test"));
        }

        var controller = new PatternController(
            context,
            Mock.Of<IImageProcessingService>(),
            colorService ?? Mock.Of<IColorQuantizationService>(),
            Mock.Of<IYarnMatchingService>(),
            Mock.Of<IPdfGenerationService>(),
            Mock.Of<IGridGenerationService>(),
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

    private static string CreateTempDir() {
        var path = Path.Combine(Path.GetTempPath(), $"stitchlens-cache-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
