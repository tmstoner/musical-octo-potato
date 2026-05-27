using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MusicalOctoPotato.Web.Tests;

public sealed class ApiWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SupportsFieldsUploadQueryAndMetricsWorkflow()
    {
        var indexName = "books";

        var defineResponse = await _client.PostAsJsonAsync($"/api/indices/{indexName}/fields", new
        {
            fields = new[] { "title", "body" }
        });
        Assert.Equal(HttpStatusCode.NoContent, defineResponse.StatusCode);

        var uploadResponse = await _client.PostAsJsonAsync($"/api/indices/{indexName}/documents", new
        {
            fields = new Dictionary<string, string>
            {
                ["title"] = "Lucene intro",
                ["body"] = "A searchable document"
            }
        });
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);

        var queryResponse = await _client.GetAsync($"/api/indices/{indexName}/query?q=Lucene");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);

        var queryPayload = await queryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, queryPayload.ValueKind);
        Assert.NotEqual(0, queryPayload.GetArrayLength());

        var metricsResponse = await _client.GetAsync("/api/metrics");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);

        var metricsPayload = await metricsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(metricsPayload.TryGetProperty("totalIndices", out var totalIndices));
        Assert.Equal(1, totalIndices.GetInt32());
    }
}
