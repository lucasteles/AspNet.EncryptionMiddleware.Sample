using CryptoMidSample;
using static Microsoft.AspNetCore.Http.TypedResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<CryptoMiddleware.Options>()
    .BindConfiguration(string.Empty);

var app = builder.Build();

app.UseMiddleware<CryptoMiddleware.Response>();
app.UseMiddleware<CryptoMiddleware.Request>();
app.UseRouting();

Greet SayHello(string name) => new($"{name}!");

app.MapGet("/hello-raw/{name}", SayHello);

app.MapGet("/hello/{name}", (string name) =>
    Json(SayHello(name), contentType: CryptoMiddleware.ContentType));

app.MapPost("/hello", (Greet greet) => SayHello(greet.Name));

app.Run();

public record Greet(string Name);