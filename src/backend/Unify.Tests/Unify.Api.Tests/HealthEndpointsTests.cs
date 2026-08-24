using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Unify.Api.Tests.Infrastructure;
using Unify.Domain.Common;

namespace Unify.Api.Tests;

/// <summary>
/// The scaffold's end-to-end proof: a 200 from /health means a request travelled through the
/// API pipeline, the Application dispatcher, an Infrastructure port and a Domain rule.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<UnifyApiFactory>
{
    private readonly UnifyApiFactory _factory;

    public HealthEndpointsTests(UnifyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Liveness_answers_without_touching_any_dependency()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_round_trips_through_every_layer()
    {
        _factory.DatabaseState = HealthState.Healthy;

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(nameof(HealthState.Healthy), body.GetProperty("status").GetString());
        Assert.Equal(
            nameof(HealthState.Healthy),
            body.GetProperty("components").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Readiness_reports_503_when_the_database_is_down()
    {
        _factory.DatabaseState = HealthState.Unhealthy;

        try
        {
            using HttpClient client = _factory.CreateClient();

            using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            _factory.DatabaseState = HealthState.Healthy;
        }
    }
}
