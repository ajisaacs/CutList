using Bunit;
using CutList.Web.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CutList.Web.Tests;

public sealed class LengthInputComponentTests
{
    [Theory]
    [InlineData(false, "12.03", "1'  0\"")]
    [InlineData(true, "12.03", "1'  0\"")]
    [InlineData(false, "12.04", "1'  0-1/16\"")]
    [InlineData(true, "12.04", "1'  0-1/16\"")]
    [InlineData(false, "0.0598", "1/16\"")]
    [InlineData(true, "0.0598", "1/16\"")]
    public async Task Typing_emits_the_unrounded_value_and_blur_changes_only_the_display(
        bool nullableMode, string typed, string expectedDisplay)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 0m));
        var expectedValue = decimal.Parse(typed, System.Globalization.CultureInfo.InvariantCulture);

        await host.Find("input").InputAsync(new() { Value = typed });

        Assert.Equal(typed, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, expectedValue, 1);

        await host.Find("input").BlurAsync(new());

        Assert.Equal(expectedDisplay, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, expectedValue, 1);
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Formatting_an_external_value_does_not_emit_a_change(bool nullableMode)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 12.04m));

        Assert.Equal("1'  0-1/16\"", host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, 12.04m, 0);
        await host.Find("input").BlurAsync(new());
        Assert.Equal("1'  0-1/16\"", host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, 12.04m, 0);
    }

    [Theory]
    [InlineData(false, false, "")]
    [InlineData(true, false, "")]
    [InlineData(false, false, " ")]
    [InlineData(true, false, " ")]
    [InlineData(false, true, "")]
    [InlineData(true, true, "")]
    [InlineData(false, true, " ")]
    [InlineData(true, true, " ")]
    public async Task Clearing_emits_zero_or_null_once_and_blur_preserves_the_optional_value(
        bool nullableMode, bool preservePrecision, string typed)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 12.03m));
        decimal? expectedValue = nullableMode ? null : 0m;

        host.Render(p => p.Add(x => x.PreservePrecision, preservePrecision));

        await host.Find("input").InputAsync(new() { Value = typed });
        AssertBinding(host, nullableMode, expectedValue, 1);
        await host.Find("input").BlurAsync(new());
        AssertBinding(host, nullableMode, expectedValue, 1);
        // Required zero rebinds normalize whitespace to blank; optional null keeps the typed blank.
        Assert.Equal(nullableMode ? typed : "", host.Find("input").GetAttribute("value"));
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_absent_optional_value_remains_blank_and_null_on_blur(bool preservePrecision)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, true)
            .Add(x => x.PreservePrecision, preservePrecision)
            .Add(x => x.Model, (decimal?)null));

        Assert.Equal("", host.Find("input").GetAttribute("value"));
        await host.Find("input").BlurAsync(new());
        Assert.Equal("", host.Find("input").GetAttribute("value"));
        AssertBinding(host, true, null, 0);
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Invalid_input_and_blur_preserve_the_model_text_and_error(bool nullableMode, bool preservePrecision)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 12.03m));

        host.Render(p => p.Add(x => x.PreservePrecision, preservePrecision));
        await host.Find("input").InputAsync(new() { Value = "not a length" });
        AssertBinding(host, nullableMode, 12.03m, 0);
        Assert.Contains("is-invalid", host.Find("input").ClassList);
        var error = host.Find(".invalid-feedback").TextContent;
        Assert.Contains("Invalid format", error);

        await host.Find("input").BlurAsync(new());

        AssertBinding(host, nullableMode, 12.03m, 0);
        Assert.Equal("not a length", host.Find("input").GetAttribute("value"));
        Assert.Contains("is-invalid", host.Find("input").ClassList);
        Assert.Equal(error, host.Find(".invalid-feedback").TextContent);
    }

    [Theory]
    [InlineData(false, "0.0598", "0.0598\"")]
    [InlineData(true, "0.0598", "0.0598\"")]
    [InlineData(false, "0.065", "0.065\"")]
    [InlineData(true, "0.065", "0.065\"")]
    [InlineData(false, "0.1196", "0.1196\"")]
    [InlineData(true, "0.1196", "0.1196\"")]
    [InlineData(false, "0.0625", "1/16\"")]
    [InlineData(true, "0.125", "1/8\"")]
    [InlineData(false, "0.0598123456789", "0.0598123456789\"")]
    [InlineData(true, "0.0625000000000000000000000001", "0.0625000000000000000000000001\"")]
    public async Task Precision_mode_displays_the_exact_bound_decimal_without_a_callback(
        bool nullableMode, string stored, string expectedDisplay)
    {
        await using var context = new BunitContext();
        var value = decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture);
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.PreservePrecision, true)
            .Add(x => x.Model, value));

        Assert.Equal(expectedDisplay, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, value, 0);
        await host.Find("input").BlurAsync(new());
        Assert.Equal(expectedDisplay, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, value, 0);
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(false, "0.0598", "0.0598\"")]
    [InlineData(true, "0.0598", "0.0598\"")]
    [InlineData(false, "0.065", "0.065\"")]
    [InlineData(true, "0.065", "0.065\"")]
    [InlineData(false, "0.1196", "0.1196\"")]
    [InlineData(true, "0.1196", "0.1196\"")]
    [InlineData(false, "0.0625", "1/16\"")]
    [InlineData(true, "0.125", "1/8\"")]
    public async Task Precision_mode_blur_preserves_the_typed_model_and_callback_count(
        bool nullableMode, string typed, string expectedDisplay)
    {
        await using var context = new BunitContext();
        var value = decimal.Parse(typed, System.Globalization.CultureInfo.InvariantCulture);
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.PreservePrecision, true)
            .Add(x => x.Model, 0m));

        await host.Find("input").InputAsync(new() { Value = typed });
        Assert.Equal(typed, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, value, 1);
        await host.Find("input").BlurAsync(new());
        Assert.Equal(expectedDisplay, host.Find("input").GetAttribute("value"));
        AssertBinding(host, nullableMode, value, 1);
    }

    [Fact]
    public async Task Changing_display_mode_reformats_the_unchanged_model_without_a_callback()
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p.Add(x => x.Model, 0.0598m));
        Assert.False(host.FindComponent<LengthInput>().Instance.PreservePrecision);
        Assert.Equal("1/16\"", host.Find("input").GetAttribute("value"));

        host.Render(p => p.Add(x => x.PreservePrecision, true));
        Assert.Equal("0.0598\"", host.Find("input").GetAttribute("value"));
        AssertBinding(host, false, 0.0598m, 0);

        host.Render(p => p.Add(x => x.PreservePrecision, false));
        Assert.Equal("1/16\"", host.Find("input").GetAttribute("value"));
        AssertBinding(host, false, 0.0598m, 0);
    }

    private static void AssertBinding(
        IRenderedComponent<LengthInputHost> host, bool nullableMode, decimal? expectedValue, int callbackCount)
    {
        Assert.Equal(expectedValue, host.Instance.Model);
        var input = host.FindComponent<LengthInput>().Instance;
        if (nullableMode)
        {
            Assert.Equal(expectedValue, input.NullableValue);
            Assert.Empty(host.Instance.RequiredChanges);
            Assert.Equal(Enumerable.Repeat(expectedValue, callbackCount), host.Instance.OptionalChanges);
        }
        else
        {
            Assert.Equal(expectedValue!.Value, input.Value);
            Assert.Equal(Enumerable.Repeat(expectedValue.Value, callbackCount), host.Instance.RequiredChanges);
            Assert.Empty(host.Instance.OptionalChanges);
        }
    }

    // A real parent binding re-renders the child with each parsed value. Supplying both
    // callbacks in nullable mode also verifies that the optional binding takes precedence.
    public sealed class LengthInputHost : ComponentBase
    {
        [Parameter] public bool NullableMode { get; set; }
        [Parameter] public bool PreservePrecision { get; set; }
        [Parameter] public decimal? Model { get; set; }
        public List<decimal> RequiredChanges { get; } = new();
        public List<decimal?> OptionalChanges { get; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<LengthInput>(0);
            builder.AddAttribute(1, nameof(LengthInput.Value), Model ?? 0m);
            builder.AddAttribute(2, nameof(LengthInput.ValueChanged),
                EventCallback.Factory.Create<decimal>(this, value =>
                {
                    Model = value;
                    RequiredChanges.Add(value);
                }));
            if (NullableMode)
            {
                builder.AddAttribute(3, nameof(LengthInput.NullableValue), Model);
                builder.AddAttribute(4, nameof(LengthInput.NullableValueChanged),
                    EventCallback.Factory.Create<decimal?>(this, value =>
                    {
                        Model = value;
                        OptionalChanges.Add(value);
                    }));
            }
            builder.AddAttribute(5, nameof(LengthInput.PreservePrecision), PreservePrecision);
            builder.CloseComponent();
        }
    }
}
