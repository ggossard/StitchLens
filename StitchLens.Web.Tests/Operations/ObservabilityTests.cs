using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StitchLens.Web.Tests.Operations;

[Trait("Category", "LaunchCritical")]
public class ObservabilityTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly WebApplicationFactory<Program> _factory;

    public ObservabilityTests(WebApplicationFactory<Program> factory) {
        _factory = factory;
    }

    private static readonly WebApplicationFactoryClientOptions ClientOptions = new() {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    };

    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess_AndCorrelationHeader() {
        using var client = _factory.CreateClient(ClientOptions);

        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();
        response.Headers.GetValues("X-Correlation-ID").Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CorrelationHeader_EchoesClientProvidedValue() {
        using var client = _factory.CreateClient(ClientOptions);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "launch-hardening-test-correlation");

        var response = await client.SendAsync(request);

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.GetValues("X-Correlation-ID")
            .Single()
            .Should()
            .Be("launch-hardening-test-correlation");
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsSuccess() {
        using var client = _factory.CreateClient(ClientOptions);

        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsSuccess() {
        using var client = _factory.CreateClient(ClientOptions);

        var response = await client.GetAsync("/health/ready");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
