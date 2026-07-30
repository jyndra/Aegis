using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aegis.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Aegis.Service.Tests;

public class FullApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FullApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPolicy_ReturnsOk_WithPolicyInfo()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostHandshake_ReturnsOk_WithSessionToken()
    {
        var client = _factory.CreateClient();
        var req = new HandshakeRequest("aegis-extension-chrome", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "HMAC123");

        var response = await client.PostAsJsonAsync("/handshake", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HandshakeResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task PostEvaluate_ReturnsOk_WithFilterDecision()
    {
        var client = _factory.CreateClient();
        var req = new EvaluationRequest(
            Url: "https://example.com",
            Domain: "example.com",
            Path: "/",
            Query: null,
            Title: "Example",
            Referrer: null,
            Browser: "Chrome",
            Component: "Extension",
            Timestamp: DateTimeOffset.UtcNow
        );

        var response = await client.PostAsJsonAsync("/evaluate", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EvaluationResult>();
        body.Should().NotBeNull();
        body!.Decision.Should().Be(FilterDecision.Allow);
    }

    [Fact]
    public async Task PostUnlockEndpoints_ReturnOk()
    {
        var client = _factory.CreateClient();

        var reqResp = await client.PostAsync("/unlock/request", null);
        reqResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var advResp = await client.PostAsync("/unlock/advance", null);
        advResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelResp = await client.PostAsync("/unlock/cancel", null);
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostIntegrityEndpoints_ReturnOk()
    {
        var client = _factory.CreateClient();

        var checkResp = await client.PostAsync("/integrity/check", null);
        checkResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var repairResp = await client.PostAsync("/repair", null);
        repairResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
