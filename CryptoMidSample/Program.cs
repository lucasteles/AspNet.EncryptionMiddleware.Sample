var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/hello/{name}",
    (string name) => new {hello = name.ToUpper()});

app.Run();