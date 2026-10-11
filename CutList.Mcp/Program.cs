using CutList.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Register HttpClient for API calls to CutList.Web
var apiUrl = Environment.GetEnvironmentVariable("CUTLIST_API_URL") ?? "http://localhost:5270";
if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri) ||
    (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps) ||
    !string.IsNullOrEmpty(apiUri.Query) || !string.IsNullOrEmpty(apiUri.Fragment))
    throw new ArgumentException("CUTLIST_API_URL must be an absolute HTTP(S) URL without a query or fragment.");

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiUri.AbsoluteUri.TrimEnd('/') + "/");
});

// Suppress all logging so it doesn't interfere with MCP stdio transport
builder.Logging.ClearProviders();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();
await app.RunAsync();
