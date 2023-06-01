using Microsoft.Net.Http.Headers;

namespace CryptoMidSample.Tests;

public class ApiTests : IDisposable
{
    readonly WebApplicationFactory<Program> app = new();
    readonly HttpClient client;

    public ApiTests()
    {
        client = app.CreateClient();
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task ShouldGetRawValue()
    {
        var name = "Peter";
        var response = await client.GetStringAsync($"/hello-raw/{name}");
        response.Should().Be(
            $$"""
            {"name":"{{name}}!"}
            """);
    }

    [Fact]
    public async Task ShouldGetEncrypted()
    {
        var name = "Peter";
        var response = await client.GetAsync($"/hello/{name}");

        response.Should()
            .BeSuccessful()
            .And.HaveHeader(HeaderNames.ContentType, "application/jose")
            .And.MatchInContent("eyJuYW1lIjoiUGV0ZXIhIn0=");
    }

    [Fact]
    public async Task ShouldTransformRequest()
    {
        var response = await client.SendAsync(
            new(HttpMethod.Post, "/hello")
            {
                Content = new StringContent(
                    "eyJuYW1lIjoiUGV0ZXIifQ==",
                    CryptoMiddleware.MediaTypeHeader
                ),
            }
        );

        response.Should()
            .BeSuccessful()
            .And.BeAs(new
            {
                name = "Peter!",
            });
    }

    [Fact]
    public async Task ShouldSendAndRetrieve()
    {
        var name = $"Peter{DateTime.UtcNow.Second}";

        var encoded = await client.GetStringAsync($"/hello/{name}");

        var response = await client.SendAsync(
            new(HttpMethod.Post, "/hello")
            {
                Content = new StringContent(
                    encoded,
                    CryptoMiddleware.MediaTypeHeader
                ),
            }
        );

        response.Should()
            .BeSuccessful()
            .And.BeAs(new
            {
                name = name + "!!",
            });
    }
}