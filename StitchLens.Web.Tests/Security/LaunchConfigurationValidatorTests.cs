using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using StitchLens.Web.Services;

namespace StitchLens.Web.Tests.Security;

public class LaunchConfigurationValidatorTests {
    [Fact]
    public void ValidateOrThrow_DoesNotThrowInDevelopment_WhenRequiredKeysMissing() {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var environment = new TestWebHostEnvironment { EnvironmentName = "Development" };

        var act = () => LaunchConfigurationValidator.ValidateOrThrow(configuration, environment, NullLogger.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrThrow_ThrowsInProduction_WhenRequiredKeysMissing() {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var environment = new TestWebHostEnvironment { EnvironmentName = "Production" };

        var act = () => LaunchConfigurationValidator.ValidateOrThrow(configuration, environment, NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Stripe:SecretKey*");
    }

    [Fact]
    public void ValidateOrThrow_DoesNotThrowInProduction_WhenRequiredKeysPresent() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=stitchlens.db",
                ["Stripe:SecretKey"] = "sk_test_123",
                ["Stripe:WebhookSecret"] = "whsec_123"
            })
            .Build();
        var environment = new TestWebHostEnvironment { EnvironmentName = "Production" };

        var act = () => LaunchConfigurationValidator.ValidateOrThrow(configuration, environment, NullLogger.Instance);

        act.Should().NotThrow();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment {
        public string ApplicationName { get; set; } = "StitchLens.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
