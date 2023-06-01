using System.Net.Http.Json;
using System.Text.Json;

namespace CryptoMidSample.Tests;

public class ApiTests : IDisposable
{
    readonly WebApplicationFactory<Program> app = new();
    readonly HttpClient client;
    readonly Faker faker = new();

    public ApiTests()
    {
        client = app.CreateClient();
        Randomizer.Seed = new(42);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task ShouldGetEncrypted()
    {
        var name = faker.Name.FirstName();
        var response = await client.GetStringAsync($"/hello/{name}");
        response.Should().Be(
            $$"""
            {"hello":"{{name.ToUpper()}}"}
            """);
    }
}