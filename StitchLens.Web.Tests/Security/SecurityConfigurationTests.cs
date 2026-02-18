using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StitchLens.Web.Controllers;

namespace StitchLens.Web.Tests.Security;

[Trait("Category", "LaunchCritical")]
public class SecurityConfigurationTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityConfigurationTests(WebApplicationFactory<Program> factory) {
        _factory = factory;
    }

    [Fact]
    public void MvcFilters_IncludeGlobalAutoValidateAntiforgeryToken() {
        var mvcOptions = _factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

        mvcOptions.Filters
            .OfType<AutoValidateAntiforgeryTokenAttribute>()
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void WebhookController_HasIgnoreAntiforgeryTokenAttribute() {
        var attributes = typeof(WebhookController)
            .GetCustomAttributes(typeof(IgnoreAntiforgeryTokenAttribute), inherit: true);

        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplicationCookie_UsesSecureHttpOnlyAndLaxSameSite() {
        var cookieOptionsMonitor = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var cookieOptions = cookieOptionsMonitor.Get(IdentityConstants.ApplicationScheme);

        cookieOptions.Cookie.HttpOnly.Should().BeTrue();
        cookieOptions.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
        cookieOptions.Cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookieOptions.ExpireTimeSpan.Should().Be(TimeSpan.FromDays(30));
        cookieOptions.SlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void IdentityLockout_IsConfiguredForAbuseProtection() {
        var identityOptions = _factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        identityOptions.Lockout.MaxFailedAccessAttempts.Should().Be(5);
        identityOptions.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(15));
    }
}
