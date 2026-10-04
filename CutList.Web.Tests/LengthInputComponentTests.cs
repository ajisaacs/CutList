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
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, " ")]
    [InlineData(true, " ")]
    public async Task Clearing_emits_zero_or_null_once_and_blur_preserves_the_optional_value(
        bool nullableMode, string typed)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 12.03m));
        decimal? expectedValue = nullableMode ? null : 0m;

        await host.Find("input").InputAsync(new() { Value = typed });
        AssertBinding(host, nullableMode, expectedValue, 1);
        await host.Find("input").BlurAsync(new());
        AssertBinding(host, nullableMode, expectedValue, 1);
        // Required zero rebinds normalize whitespace to blank; optional null keeps the typed blank.
        Assert.Equal(nullableMode ? typed : "", host.Find("input").GetAttribute("value"));
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Fact]
    public async Task An_absent_optional_value_remains_blank_and_null_on_blur()
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, true)
            .Add(x => x.Model, (decimal?)null));

        Assert.Equal("", host.Find("input").GetAttribute("value"));
        await host.Find("input").BlurAsync(new());
        Assert.Equal("", host.Find("input").GetAttribute("value"));
        AssertBinding(host, true, null, 0);
        Assert.Empty(host.FindAll(".invalid-feedback"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_input_and_blur_preserve_the_model_text_and_error(bool nullableMode)
    {
        await using var context = new BunitContext();
        var host = context.Render<LengthInputHost>(p => p
            .Add(x => x.NullableMode, nullableMode)
            .Add(x => x.Model, 12.03m));

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
            builder.CloseComponent();
        }
    }
}
