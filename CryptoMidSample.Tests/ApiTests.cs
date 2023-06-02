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
            .And.MatchInContent("X5JZf36r9HVPSiVSSN0llJGLfJg+QFvCLgKfcdp1GjU=");
    }

    [Fact]
    public async Task ShouldTransformRequest()
    {
        var response = await client.SendAsync(
            new(HttpMethod.Post, "/hello")
            {
                Content = new StringContent(
                    "A14VJmXsfiznGXWPvqTSgd1hTjp92K1nS4nYDUKMvg4=",
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