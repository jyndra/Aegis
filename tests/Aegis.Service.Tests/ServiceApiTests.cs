using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Aegis.Service.Tests;

public class ServiceApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOk_WithHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStatusReport_ReturnsOk_WithSubsystemReports()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/status/report");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("protectionState").GetString().Should().BeOneOf("Protected", "Degraded");
        json.RootElement.GetProperty("subsystems").GetArrayLength().Should().BeGreaterThan(0);
    }
}
