using joelbyford;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5057");

var app = builder.Build();

var users = new Dictionary<string, string>
{
    ["demoUser"] = "demoPass!123"
};

app.UseMiddleware<BasicAuth>("basic-auth-harness", users);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/echo", () => Results.Ok(new { status = "authenticated" }));

app.Run();
