using System.Net;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using CutList.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class StaticAssetTests
{
    private readonly SqlServerFixture _db;

    public StaticAssetTests(SqlServerFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task Declared_favicon_is_served_as_nonempty_svg()
    {
        await _db.ResetAsync();
        // The shared host uses Testing, so explicitly load the build's static-assets manifest.
        // Keep the real application's static-file middleware and request pipeline.
        using var factory = _db.Factory.WithWebHostBuilder(builder => builder.UseStaticWebAssets());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var document = await new HtmlParser().ParseDocumentAsync(await page.Content.ReadAsStringAsync());
        var icon = Assert.Single(document.QuerySelectorAll("head link[rel~='icon']"));
        var href = icon.GetAttribute("href");
        Assert.False(string.IsNullOrWhiteSpace(href));
        var pageUrl = page.RequestMessage!.RequestUri!;
        var baseUrl = new Uri(pageUrl, document.QuerySelector("base[href]")?.GetAttribute("href") ?? pageUrl.ToString());
        var iconUrl = new Uri(baseUrl, href);
        Assert.Equal(pageUrl.Authority, iconUrl.Authority);

        // Request exactly the URL declared by the rendered app, not a hard-coded asset path.
        using var response = await client.GetAsync(iconUrl);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Declared favicon {iconUrl} returned {(int)response.StatusCode} ({response.StatusCode}).");
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("image/svg+xml", icon.GetAttribute("type"));
        var svg = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(svg));
        Assert.DoesNotContain("<html", svg, StringComparison.OrdinalIgnoreCase);
        var root = XDocument.Parse(svg).Root;
        Assert.NotNull(root);
        Assert.Equal(XName.Get("svg", "http://www.w3.org/2000/svg"), root.Name);
        Assert.NotEmpty(root.Elements());
    }
}
