using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace StitchLens.Web.Tests.Operations;

[Trait("Category", "LaunchCritical")]
public class ObservabilityTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly WebApplicationFactory<Program> _factory;
    private const string CorrelationHeaderName = "X-Correlation-ID";

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

        using var response = await SendGetWithSingleRedirectAsync(client, "/health");

        await AssertSuccessAsync(response, "/health");
        response.Headers.Contains(CorrelationHeaderName).Should().BeTrue();
        response.Headers.GetValues(CorrelationHeaderName).Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CorrelationHeader_EchoesClientProvidedValue() {
        using var client = _factory.CreateClient(ClientOptions);

        const string correlationId = "launch-hardening-test-correlation";

        using var response = await SendGetWithSingleRedirectAsync(client, "/health", correlationId);

        await AssertSuccessAsync(response, "/health");
        response.Headers.GetValues(CorrelationHeaderName)
            .Single()
            .Should()
            .Be(correlationId);
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsSuccess() {
        using var client = _factory.CreateClient(ClientOptions);

        using var response = await SendGetWithSingleRedirectAsync(client, "/health/live");

        await AssertSuccessAsync(response, "/health/live");
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsSuccess() {
        using var client = _factory.CreateClient(ClientOptions);

        using var response = await SendGetWithSingleRedirectAsync(client, "/health/ready");

        await AssertSuccessAsync(response, "/health/ready");
    }

    private static async Task<HttpResponseMessage> SendGetWithSingleRedirectAsync(HttpClient client, string path, string? correlationId = null) {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(correlationId)) {
            request.Headers.Add(CorrelationHeaderName, correlationId);
        }

        var response = await client.SendAsync(request);
        if (!IsRedirectStatusCode(response.StatusCode) || response.Headers.Location == null) {
            return response;
        }

        var redirectUri = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location
            : new Uri(client.BaseAddress!, response.Headers.Location);

        response.Dispose();

        var redirectedRequest = new HttpRequestMessage(HttpMethod.Get, redirectUri);
        if (!string.IsNullOrWhiteSpace(correlationId)) {
            redirectedRequest.Headers.Add(CorrelationHeaderName, correlationId);
        }

        return await client.SendAsync(redirectedRequest);
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) {
        var code = (int)statusCode;
        return code is >= 300 and < 400;
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response, string path) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected successful response from {path} but got {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }
}
