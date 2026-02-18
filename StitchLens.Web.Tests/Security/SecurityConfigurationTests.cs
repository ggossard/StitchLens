using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StitchLens.Web.Controllers;

namespace StitchLens.Web.Tests.Security;

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
}
