using Bunit;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EditJobPage = CutList.Web.Components.Pages.Jobs.Edit;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class JobPresentationComponentTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private BunitContext _ctx = null!;

    public JobPresentationComponentTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(_db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        _ctx.Services.AddSingleton(_db.Services.GetRequiredService<IPackingEngineCatalog>());
        _ctx.Services.AddScoped<JobService>();
        _ctx.Services.AddScoped<MaterialService>();
        _ctx.Services.AddScoped<CutListPackingService>();
        _ctx.Services.AddScoped<ReportService>();
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(1, "1 bar")]
    [InlineData(2, "2 bars")]
    public async Task Material_list_footer_uses_the_used_bar_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Equal(expected, page.Find(".print-material-list tfoot td.text-end").TextContent.Trim());
    }

    [Theory]
    [InlineData(1, ".cutlist-material-screen-header .text-muted", "1 bar · 1 piece")]
    [InlineData(1, ".cutlist-material-print-header .cutlist-material-stats", "1 bar · 1 piece")]
    [InlineData(2, ".cutlist-material-screen-header .text-muted", "2 bars · 2 pieces")]
    [InlineData(2, ".cutlist-material-print-header .cutlist-material-stats", "2 bars · 2 pieces")]
    public async Task Material_headers_use_the_used_bar_and_piece_counts(int quantity, string selector, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var stats = NormalizeWhitespace(page.Find(selector).TextContent).Split(" · ");
        Assert.Equal(expected, string.Join(" · ", stats.Take(2)));
    }

    [Theory]
    [InlineData(1, "Total: 1 piece")]
    [InlineData(2, "Total: 2 pieces")]
    public async Task Parts_total_uses_the_piece_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Parts");

        Assert.Equal(expected, NormalizeWhitespace(page.Find(".tab-content .mt-3.text-muted").TextContent));
    }

    [Theory]
    [InlineData(1, "1 item not placed")]
    [InlineData(2, "2 items not placed")]
    public async Task Unplaced_warning_uses_the_item_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity, hasStock: false);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Equal(expected, page.Find(".cutlist-material-card .alert-danger strong").TextContent.Trim());
        foreach (var selector in new[] { ".cutlist-material-screen-header .text-muted", ".cutlist-material-print-header .cutlist-material-stats" })
        {
            var stats = NormalizeWhitespace(page.Find(selector).TextContent).Split(" · ");
            Assert.Equal("0 bars · 0 pieces", string.Join(" · ", stats.Take(2)));
        }
    }

    [Theory]
    [InlineData("Parts", "144.0000", "(144\")")]
    [InlineData("Parts", "240.0000", "(240\")")]
    [InlineData("Parts", "12.0625", "(12.0625\")")]
    [InlineData("Stock", "144.0000", "(144\")")]
    [InlineData("Stock", "240.0000", "(240\")")]
    [InlineData("Stock", "12.0625", "(12.0625\")")]
    public async Task Secondary_lengths_strip_only_insignificant_database_zeros(string tab, string storedLength, string expected)
    {
        var length = decimal.Parse(storedLength, System.Globalization.CultureInfo.InvariantCulture);
        var jobId = await CreateSavedJobAsync(partLength: length, stockLength: length);
        var snapshot = await JobSnapshot.CaptureAsync(_db);
        Assert.Equal(length, Assert.Single(snapshot.PartsOf(jobId)).LengthInches);
        Assert.Equal(length, Assert.Single(snapshot.StockOf(jobId)).LengthInches);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, tab);

        Assert.Equal(expected, page.Find(".tab-content tbody td:nth-child(2) .text-muted").TextContent.Trim());
    }

    [Fact]
    public async Task Rendering_and_unit_toggles_preserve_the_entire_locked_saved_job()
    {
        var jobId = await CreateSavedJobAsync(partLength: 12.0625m);
        var before = await JobSnapshot.CaptureAsync(_db);
        Assert.NotNull(before.Job(jobId).LockedAt);
        Assert.NotNull(before.Job(jobId).OptimizedAt);
        Assert.NotNull(before.Job(jobId).OptimizationResultJson);
        var page = await RenderEditorAsync(jobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await ClickTabAsync(page, "Parts");
        await ClickTabAsync(page, "Stock");
        await ClickTabAsync(page, "Results");
        var initialLength = page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent;
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show feet + inches").ClickAsync(new());
        Assert.NotEqual(initialLength, page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show all inches").ClickAsync(new());
        Assert.Equal(initialLength, page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Results_toolbar_keeps_complete_controls_as_direct_items(bool locked)
    {
        var jobId = await CreateSavedJobAsync(locked: locked);
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var toolbar = page.Find(".results-toolbar");
        var picker = toolbar.QuerySelector(":scope > .packing-engine-picker")!;
        Assert.NotNull(picker);
        Assert.Equal("Engine", picker.QuerySelector("label[for='packing-engine']")!.TextContent.Trim());
        Assert.Equal(locked, picker.QuerySelector("#packing-engine")!.HasAttribute("disabled"));
        var optimize = toolbar.QuerySelector(":scope > #run-optimization")!;
        Assert.NotNull(optimize);
        Assert.Equal("Re-Optimize", NormalizeWhitespace(optimize.TextContent));
        Assert.Equal(locked, optimize.HasAttribute("disabled"));
        var buttons = toolbar.QuerySelectorAll(":scope > button");
        Assert.Contains(buttons, b => NormalizeWhitespace(b.TextContent) == "Print Report");
        var units = Assert.Single(buttons, b => NormalizeWhitespace(b.TextContent) == "Show feet + inches");
        Assert.True(units.HasAttribute("aria-pressed"));
        Assert.Equal(!locked, buttons.Any(b => NormalizeWhitespace(b.TextContent) == "Lock Job"));
        Assert.Equal(
            NormalizeWhitespace($"Last optimized: {before.Job(jobId).OptimizedAt!.Value.ToLocalTime():g}"),
            NormalizeWhitespace(toolbar.QuerySelector(":scope > .results-optimized-at")!.TextContent));
        Assert.NotNull(toolbar.QuerySelector(":scope > .packing-engine-used"));
        Assert.Equal(locked ? 6 : 7, toolbar.Children.Length);
        Assert.All(toolbar.Children, item => Assert.DoesNotContain(item.ClassList,
            name => name.StartsWith("ms-") || name.StartsWith("me-")));
        await units.ClickAsync(new());
        Assert.Contains(page.FindAll(".results-toolbar > button"), b => NormalizeWhitespace(b.TextContent) == "Show all inches");
        await page.FindAll(".results-toolbar > button").Single(b => NormalizeWhitespace(b.TextContent) == "Print Report").ClickAsync(new());
        _ctx.JSInterop.VerifyInvoke("printWithTitle");
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData("Exhaustive")]
    [InlineData("Exhaustive (First Fit fallback)")]
    public async Task Results_engine_label_stays_with_the_first_word_of_the_saved_value(string engineName)
    {
        var jobId = await CreateSavedJobAsync(engineName: engineName);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var used = page.Find(".results-toolbar > .packing-engine-used");
        // Keep the label and first word in a no-wrap prefix, while ordinary spaces in
        // the fallback suffix remain available as narrow-screen wrap opportunities.
        Assert.Equal($"Engine: {engineName.Split(' ', 2)[0]}", used.QuerySelector(".packing-engine-used-prefix")!.TextContent);
        Assert.Equal($"Engine: {engineName}", used.TextContent.Trim());
    }

    [Theory]
    [InlineData("exhaustive")]
    [InlineData("firstfit")]
    [InlineData("bestfit")]
    public async Task Engine_help_is_a_closed_native_disclosure_with_the_complete_selected_description(string engineId)
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Results");
        await page.Find("#packing-engine").ChangeAsync(new() { Value = engineId });
        var engine = _ctx.Services.GetRequiredService<IPackingEngineCatalog>().Engines.Single(e => e.Id == engineId);

        var help = page.Find("details.packing-engine-help");
        Assert.False(help.HasAttribute("open"));
        Assert.Contains("print-screen-only", help.ClassList);
        var summary = help.QuerySelector(":scope > summary")!;
        Assert.NotNull(summary);
        Assert.Same(summary, help.FirstElementChild);
        Assert.Equal("About this engine", summary.TextContent.Trim());
        Assert.False(summary.HasAttribute("tabindex")); // Keep native summary keyboard behavior.
        Assert.False(summary.HasAttribute("role"));
        Assert.Equal(engine.Description, help.QuerySelector(".packing-engine-description")!.TextContent.Trim());
        Assert.Single(page.FindAll(".packing-engine-description"));
        Assert.Equal(engine.Description, page.Find("#packing-engine").GetAttribute("title"));
        Assert.Equal("Engine", page.Find("label[for='packing-engine']").TextContent.Trim());
    }

    [Fact]
    public async Task Results_toolbar_preserves_optimize_before_a_plan_is_saved()
    {
        await _ctx.Services.GetRequiredService<JobService>().ClearOptimizationResultAsync(_seed.UnlockedJobId);
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Results");

        var toolbar = page.Find(".results-toolbar");
        Assert.Equal("Optimize", NormalizeWhitespace(toolbar.QuerySelector(":scope > #run-optimization")!.TextContent));
        Assert.NotNull(toolbar.QuerySelector(":scope > .packing-engine-picker"));
        Assert.Empty(toolbar.QuerySelectorAll(".packing-engine-used"));
        Assert.DoesNotContain(toolbar.QuerySelectorAll("button"), b => NormalizeWhitespace(b.TextContent) == "Print Report");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Material_list_includes_every_used_bar_once_in_material_display_identity_and_descending_length_order(
        bool newlyPacked, bool legacy)
    {
        var fixture = await CreateMixedSourceJobAsync(save: !newlyPacked, legacy: legacy);
        var beforeRender = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(fixture.JobId);
        JobSnapshot.AssertUnchanged(beforeRender, await JobSnapshot.CaptureAsync(_db));
        await ClickTabAsync(page, "Results");
        JobSnapshot.AssertUnchanged(beforeRender, await JobSnapshot.CaptureAsync(_db));
        if (newlyPacked)
        {
            await page.Find("#packing-engine").ChangeAsync(new() { Value = "firstfit" });
            await page.Find("#run-optimization").ClickAsync(new());
        }

        var before = await JobSnapshot.CaptureAsync(_db);
        var savedJson = before.Job(fixture.JobId).OptimizationResultJson!;
        var packing = _ctx.Services.GetRequiredService<CutListPackingService>();
        var loaded = (await packing.LoadSavedResultAsync(savedJson))!;
        AssertMixedPlanSources(loaded, fixture.DuplicateMaterialId);
        Assert.Equal("firstfit", loaded.EngineId);
        Assert.Equal("First Fit", loaded.EngineName);
        if (legacy)
        {
            Assert.DoesNotContain("EngineId", savedJson);
            Assert.DoesNotContain("EngineName", savedJson);
        }
        else
        {
            Assert.Contains("\"EngineId\":\"firstfit\"", savedJson);
        }
        Assert.Contains("\"InStockBins\"", savedJson);
        Assert.Contains("\"ToBePurchasedBins\"", savedJson);
        Assert.Equal(!newlyPacked, before.Job(fixture.JobId).LockedAt.HasValue);
        Assert.Contains(before.StockOf(fixture.JobId), s => s.Quantity == -1 && s.Priority == 3 && s.StockItemId == null);
        Assert.Contains(before.StockOf(fixture.JobId), s => s.Quantity == 2 && s.Priority == 2 && s.StockItemId == _seed.FlatBarStockItemId);
        var expectedRows = new[]
        {
            (MaterialId: _seed.FlatBarMaterialId, Length: 240d, Quantity: 4),
            (MaterialId: _seed.FlatBarMaterialId, Length: 120d, Quantity: 1),
            (MaterialId: fixture.DuplicateMaterialId, Length: 240d, Quantity: 1),
            (MaterialId: _seed.RoundTubeMaterialId, Length: 288d, Quantity: 1)
        };
        AssertUsedMaterialRows(page, loaded, expectedRows, feetAndInches: false);
        Assert.Equal("7 bars", page.Find(".print-material-list tfoot td.text-end").TextContent.Trim());
        Assert.Equal("Total Bars Used", page.Find(".print-material-list tfoot td:first-child").TextContent.Trim());
        Assert.Equal("Stock bars used in this cutting plan.", page.Find(".print-material-list .material-list-scope").TextContent.Trim());
        Assert.Contains(page.FindAll(".cutlist-material-card .alert-danger strong"), e => e.TextContent.Trim() == "1 item not placed");
        Assert.Contains("Items Not Placed", page.Markup);
        Assert.DoesNotContain("360\"", page.Find(".print-material-list").TextContent);

        // Screen and print use one shared table, not two independently filtered summaries.
        var list = Assert.Single(page.FindAll(".print-material-list"));
        Assert.DoesNotContain("print-screen-only", list.ClassList);
        Assert.DoesNotContain("print-only", list.ClassList);
        Assert.Empty(list.QuerySelectorAll("table .print-screen-only, table .print-only"));
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Print Report").ClickAsync(new());
        _ctx.JSInterop.VerifyInvoke("printWithTitle");
        AssertUsedMaterialRows(page, loaded, expectedRows, feetAndInches: false);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show feet + inches").ClickAsync(new());
        AssertUsedMaterialRows(page, loaded, expectedRows, feetAndInches: true);
        Assert.Equal("7 bars", page.Find(".print-material-list tfoot td.text-end").TextContent.Trim());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show all inches").ClickAsync(new());
        AssertUsedMaterialRows(page, loaded, expectedRows, feetAndInches: false);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(false, 1, "1 bar")]
    [InlineData(false, 2, "2 bars")]
    [InlineData(true, 1, "1 bar")]
    [InlineData(true, 2, "2 bars")]
    public async Task Material_list_handles_catalog_only_and_custom_only_used_bars(bool custom, int quantity, string total)
    {
        var jobId = await CreateSavedJobAsync(quantity, catalogStock: !custom);
        var before = await JobSnapshot.CaptureAsync(_db);
        var packing = _ctx.Services.GetRequiredService<CutListPackingService>();
        var loaded = (await packing.LoadSavedResultAsync(before.Job(jobId).OptimizationResultJson!))!;
        var material = Assert.Single(loaded.MaterialResults);
        Assert.Equal(quantity, material.PackResult.Bins.Count);
        Assert.Equal(custom ? 0 : quantity, material.InStockBins.Count);
        Assert.Equal(custom ? quantity : 0, material.ToBePurchasedBins.Count);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        AssertUsedMaterialRows(page, loaded, [(_seed.FlatBarMaterialId, 240d, quantity)], feetAndInches: false);
        Assert.Equal(total, page.Find(".print-material-list tfoot td.text-end").TextContent.Trim());
        Assert.Equal("Total Bars Used", page.Find(".print-material-list tfoot td:first-child").TextContent.Trim());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Material_list_zero_used_plan_has_the_exact_empty_message_and_keeps_unplaced_warnings()
    {
        var jobId = await CreateSavedJobAsync(partLength: 300m);
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Equal("Stock bars used in this cutting plan.", page.Find(".print-material-list .material-list-scope").TextContent.Trim());
        Assert.Equal("No stock bars were used in this plan.", page.Find(".print-material-list .card-body p:last-child").TextContent.Trim());
        Assert.Empty(page.FindAll(".print-material-list tbody tr"));
        Assert.Equal("1 item not placed", page.Find(".cutlist-material-card .alert-danger strong").TextContent.Trim());
        Assert.Contains("Items Not Placed", page.Markup);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Stock_source_and_empty_catalog_modal_use_catalog_terminology_without_mutation()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Stock");

        Assert.Equal("Catalog", page.Find(".tab-content tbody td:nth-child(5) .badge").TextContent.Trim());
        Assert.DoesNotContain("inventory", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Add Stock").ClickAsync(new());
        Assert.Equal("From Catalog", page.Find(".modal .nav-link.active").TextContent.Trim());
        Assert.Contains("No matching catalog stock found.", page.Find(".modal").TextContent);
        Assert.DoesNotContain("inventory", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.FindAll(".modal button").Single(b => b.TextContent.Trim() == "Cancel").ClickAsync(new());
        await page.Find(".tab-content button[title='Edit']").ClickAsync(new());
        Assert.Equal(_seed.FlatBarStockItemId.ToString(), page.FindAll(".modal select")[2].GetAttribute("value"));
        Assert.DoesNotContain("inventory", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.FindAll(".modal button").Single(b => b.TextContent.Trim() == "Cancel").ClickAsync(new());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stock_empty_help_and_add_modal_use_catalog_terminology(bool hasParts)
    {
        var jobs = _ctx.Services.GetRequiredService<JobService>();
        var job = await jobs.CreateAsync(new Job { Name = "Catalog help fixture", CuttingToolId = 1 });
        if (hasParts)
        {
            await jobs.AddPartAsync(new JobPart
            {
                JobId = job.Id, MaterialId = _seed.FlatBarMaterialId, Name = "Brace", LengthInches = 30m, Quantity = 1
            });
        }
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(job.Id);
        await ClickTabAsync(page, "Stock");
        Assert.Contains("Add stock from your catalog or define custom lengths.", page.Find(".tab-content").TextContent);
        Assert.Contains("there is no automatic fallback to catalog.", page.Find(".tab-content").TextContent);
        Assert.DoesNotContain("inventory", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Add Stock").ClickAsync(new());
        var catalogTab = page.FindAll(".modal .nav-link").Single(e => e.TextContent.Trim() == "From Catalog");
        Assert.Equal(!hasParts, catalogTab.HasAttribute("disabled"));
        Assert.Equal(hasParts ? "" : "Add parts first to match against catalog", catalogTab.GetAttribute("title"));
        if (hasParts)
        {
            Assert.Contains("active", catalogTab.ClassList);
            Assert.Single(page.FindAll(".modal tbody tr"));
            Assert.Equal("-1", page.Find(".modal input[type='number']").GetAttribute("value"));
        }
        Assert.DoesNotContain("inventory", page.Markup, StringComparison.OrdinalIgnoreCase);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(false, "Exhaustive")]
    [InlineData(true, "Exhaustive")]
    [InlineData(false, "Exhaustive (First Fit fallback)")]
    [InlineData(true, "Exhaustive (First Fit fallback)")]
    public async Task Printed_results_include_saved_metadata_and_safe_multiline_notes_without_duplicate_title(bool locked, string engineName)
    {
        const string notes = "Check both ends.\n<script>window.injected = true</script>\nUse <b>labels</b> & keep drops.";
        var jobId = await CreateSavedJobAsync(locked: locked, engineName: engineName, customer: "Print customer", notes: notes);
        var before = await JobSnapshot.CaptureAsync(_db);
        var savedJob = before.Job(jobId);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        // A different picker selection must not relabel the already-saved plan.
        if (!locked)
            await page.Find("#packing-engine").ChangeAsync(new() { Value = "bestfit" });
        var title = Assert.Single(page.FindAll("h1.job-title"));
        Assert.Equal($"{savedJob.JobNumber} - {savedJob.Name}", title.TextContent);
        var metadata = Assert.Single(page.FindAll(".print-job-metadata"));
        Assert.Empty(metadata.QuerySelectorAll("h1, h2"));
        Assert.Equal("Customer: Print customer", NormalizeWhitespace(metadata.QuerySelector(".print-customer")!.TextContent));
        Assert.Equal($"Engine: {engineName}", NormalizeWhitespace(metadata.QuerySelector(".print-engine-used")!.TextContent));
        Assert.Equal(NormalizeWhitespace($"Last optimized: {savedJob.OptimizedAt!.Value.ToLocalTime():g}"),
            NormalizeWhitespace(metadata.QuerySelector(".print-optimized-at")!.TextContent));
        var timestamp = Assert.Single(metadata.QuerySelectorAll("time[data-print-timestamp]"));
        Assert.Equal(string.Empty, timestamp.TextContent);
        Assert.False(timestamp.HasAttribute("datetime")); // The browser fills this on every print attempt, not page load.
        Assert.True(page.Markup.IndexOf("class=\"print-job-metadata\"", StringComparison.Ordinal)
            < page.Markup.IndexOf("class=\"row mb-4 print-summary\"", StringComparison.Ordinal));
        Assert.Contains("print-screen-only", page.Find(".results-optimized-at").ClassList);
        Assert.Contains("print-screen-only", page.Find(".packing-engine-used").ClassList);
        var method = Assert.Single(page.FindAll(".print-cut-method"));
        Assert.Equal("Cut Method: Bandsaw Kerf: 1/16\"", NormalizeWhitespace(method.TextContent));

        var printedNotes = Assert.Single(page.FindAll(".print-job-notes"));
        Assert.Equal("Notes", printedNotes.QuerySelector("h3")!.TextContent);
        Assert.Equal(notes, printedNotes.QuerySelector("p")!.TextContent);
        Assert.Empty(printedNotes.QuerySelectorAll("script, b"));
        Assert.Contains("&lt;script&gt;", printedNotes.InnerHtml);
        Assert.Same(printedNotes, page.Find(".tab-content").LastElementChild);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public async Task Printed_results_omit_blank_customer_and_notes(string? blank)
    {
        var jobId = await CreateSavedJobAsync(customer: blank, notes: blank);
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Single(page.FindAll(".print-job-metadata"));
        Assert.Empty(page.FindAll(".print-customer, .print-job-notes"));
        Assert.Single(page.FindAll("time[data-print-timestamp]"));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_print_actions_preserve_the_entire_saved_job(bool locked)
    {
        var jobId = await CreateSavedJobAsync(locked: locked, customer: "Repeat print", notes: "Keep this note.");
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await page.FindAll("button").Single(b => b.TextContent.Trim() == "Print Report").ClickAsync(new());
            _ctx.JSInterop.VerifyInvoke("printWithTitle", attempt);
            JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
        }
    }

    private static void AssertUsedMaterialRows(
        IRenderedComponent<EditJobPage> page, MultiMaterialPackResult result,
        (int MaterialId, double Length, int Quantity)[] expected, bool feetAndInches)
    {
        var rows = page.FindAll(".print-material-list tbody tr");
        Assert.Equal(expected.Length, rows.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            var row = expected[i];
            var cells = rows[i].QuerySelectorAll("td");
            Assert.Equal(result.MaterialResults.Single(m => m.Material.Id == row.MaterialId).Material.DisplayName, cells[0].TextContent.Trim());
            Assert.Equal(feetAndInches
                ? CutList.Core.Formatting.ArchUnits.FormatFromInches(row.Length)
                : CutList.Core.Formatting.ArchUnits.FormatInches(row.Length), cells[1].TextContent.Trim());
            Assert.Equal(row.Quantity.ToString(), cells[2].TextContent.Trim());
        }
    }

    private void AssertMixedPlanSources(MultiMaterialPackResult result, int duplicateMaterialId)
    {
        // Packing and saved JSON retain source order, not the Material List's display/ID order.
        Assert.Equal(new[] { _seed.RoundTubeMaterialId, duplicateMaterialId, _seed.FlatBarMaterialId },
            result.MaterialResults.Select(m => m.Material.Id));
        Assert.True(_seed.FlatBarMaterialId < duplicateMaterialId);
        Assert.Equal(7, result.MaterialResults.Sum(m => m.PackResult.Bins.Count));
        var flat = result.MaterialResults.Single(m => m.Material.Id == _seed.FlatBarMaterialId);
        Assert.Equal(2, flat.InStockBins.Count(b => b.Length == 240));
        Assert.Equal(2, flat.ToBePurchasedBins.Count(b => b.Length == 240));
        Assert.Single(flat.ToBePurchasedBins, b => b.Length == 120);
        Assert.Equal(new[] { 120d, 240d, 240d, 240d, 240d }, flat.PackResult.Bins.Select(b => b.Length).Order());
        Assert.Equal(500d, Assert.Single(flat.PackResult.ItemsNotUsed).Length);
        Assert.Equal(flat.Material.DisplayName, result.MaterialResults.Single(m => m.Material.Id == duplicateMaterialId).Material.DisplayName);
        Assert.All(result.MaterialResults, material =>
        {
            Assert.Equal(material.PackResult.Bins.Count, material.InStockBins.Count + material.ToBePurchasedBins.Count);
            Assert.All(material.InStockBins.Concat(material.ToBePurchasedBins), bin => Assert.Contains(bin, material.PackResult.Bins));
        });
    }

    private async Task<(int JobId, int DuplicateMaterialId)> CreateMixedSourceJobAsync(bool save, bool legacy)
    {
        await using var context = await _db.CreateContextAsync();
        var duplicate = new Material
        {
            Shape = MaterialShape.FlatBar, Type = MaterialType.Aluminum, Grade = "6061",
            Size = "1/4 x 2", SortOrder = 2000
        };
        context.Materials.Add(duplicate);
        await context.SaveChangesAsync();
        var jobs = _ctx.Services.GetRequiredService<JobService>();
        var job = await jobs.CreateAsync(new Job { Name = "Seven actually used bars", CuttingToolId = 1 });
        // Start with Round Tube and reverse the equal-display-name Flat Bar identities so both
        // display-name ordering and the material-ID tie-breaker must override source order.
        foreach (var part in new[]
        {
            new JobPart { MaterialId = _seed.RoundTubeMaterialId, Name = "Tube", LengthInches = 180m, Quantity = 1 },
            new JobPart { MaterialId = duplicate.Id, Name = "Other grade", LengthInches = 150m, Quantity = 1 },
            new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Rail", LengthInches = 150m, Quantity = 4 },
            new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Brace", LengthInches = 90m, Quantity = 1 },
            new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Too long", LengthInches = 500m, Quantity = 1 }
        })
        {
            part.JobId = job.Id;
            await jobs.AddPartAsync(part);
        }
        foreach (var stock in new[]
        {
            new JobStock { MaterialId = _seed.RoundTubeMaterialId, StockItemId = _seed.RoundTubeStockItemId, LengthInches = 288m, Quantity = -1, Priority = 5 },
            new JobStock { MaterialId = _seed.FlatBarMaterialId, IsCustomLength = true, LengthInches = 120m, Quantity = 1, Priority = 1 },
            new JobStock { MaterialId = _seed.FlatBarMaterialId, StockItemId = _seed.FlatBarStockItemId, LengthInches = 240m, Quantity = 2, Priority = 2 },
            new JobStock { MaterialId = _seed.FlatBarMaterialId, IsCustomLength = true, LengthInches = 240m, Quantity = -1, Priority = 3 },
            new JobStock { MaterialId = _seed.FlatBarMaterialId, IsCustomLength = true, LengthInches = 360m, Quantity = -1, Priority = 4 },
            new JobStock { MaterialId = duplicate.Id, IsCustomLength = true, LengthInches = 240m, Quantity = -1, Priority = 7 }
        })
        {
            stock.JobId = job.Id;
            await jobs.AddStockAsync(stock);
        }
        job = (await jobs.GetByIdAsync(job.Id))!;
        var packing = _ctx.Services.GetRequiredService<CutListPackingService>();
        var result = await packing.PackAsync(job.Parts, job.CuttingTool!.KerfInches, job.Stock, "firstfit");
        AssertMixedPlanSources(result, duplicate.Id);
        if (save)
        {
            var json = packing.SerializeResult(result);
            if (legacy)
            {
                var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
                legacyJson.Remove("EngineId");
                legacyJson.Remove("EngineName");
                json = legacyJson.ToJsonString();
            }
            await jobs.SaveOptimizationResultAsync(job.Id, json, DateTime.UtcNow);
            await jobs.LockAsync(job.Id);
        }
        return (job.Id, duplicate.Id);
    }

    private async Task<int> CreateSavedJobAsync(
        int quantity = 1, bool hasStock = true, decimal partLength = 144.0000m, decimal stockLength = 240.0000m,
        bool locked = true, string? engineName = null, bool catalogStock = false, string? customer = null, string? notes = null)
    {
        var jobs = _ctx.Services.GetRequiredService<JobService>();
        var packing = _ctx.Services.GetRequiredService<CutListPackingService>();
        var job = await jobs.CreateAsync(new Job { Name = "Presentation fixture", CuttingToolId = 1, Customer = customer, Notes = notes });
        await jobs.AddPartAsync(new JobPart
        {
            JobId = job.Id,
            MaterialId = _seed.FlatBarMaterialId,
            Name = "Presentation part",
            LengthInches = partLength,
            Quantity = quantity
        });
        if (hasStock)
        {
            await jobs.AddStockAsync(new JobStock
            {
                JobId = job.Id,
                MaterialId = _seed.FlatBarMaterialId,
                StockItemId = catalogStock ? _seed.FlatBarStockItemId : null,
                IsCustomLength = !catalogStock,
                LengthInches = stockLength,
                Quantity = quantity,
                Priority = 1
            });
        }

        // Pack freshly loaded persisted rows and save the actual source partitions.
        job = (await jobs.GetByIdAsync(job.Id))!;
        var result = await packing.PackAsync(job.Parts, job.CuttingTool!.KerfInches, job.Stock);
        var materialResult = Assert.Single(result.MaterialResults);
        var usedBars = hasStock && partLength <= stockLength ? quantity : 0;
        Assert.Equal(catalogStock ? usedBars : 0, materialResult.InStockBins.Count);
        Assert.Equal(catalogStock ? 0 : usedBars, materialResult.ToBePurchasedBins.Count);
        Assert.Equal(usedBars > 0 ? 0 : quantity, materialResult.PackResult.ItemsNotUsed.Count);
        if (engineName != null)
        {
            // Exercise saved display metadata, including the real catalog's longest fallback name;
            // this presentation fixture does not attempt to force the packing search to time out.
            Assert.Contains(engineName, new[]
            {
                BuiltInPackingEngines.Exhaustive.DisplayName,
                BuiltInPackingEngines.Exhaustive.RunName(BuiltInPackingEngines.FirstFit)
            });
            result.EngineName = engineName;
        }
        await jobs.SaveOptimizationResultAsync(job.Id, packing.SerializeResult(result), DateTime.UtcNow);
        if (locked)
        {
            await jobs.LockAsync(job.Id);
        }
        return job.Id;
    }

    private async Task<IRenderedComponent<EditJobPage>> RenderEditorAsync(int jobId)
    {
        var page = _ctx.Render<EditJobPage>(p => p.Add(x => x.Id, jobId));
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("ul.nav-tabs")));
        return page;
    }

    private static Task ClickTabAsync(IRenderedComponent<EditJobPage> page, string tab) =>
        page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith(tab)).ClickAsync(new());

    private static string NormalizeWhitespace(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
