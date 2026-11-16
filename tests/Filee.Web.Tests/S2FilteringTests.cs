using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using Filee.Web.Conversion;

namespace Filee.Web.Tests;

public sealed class S2FilteringTests
{
    [Fact]
    public void WebConversionAssembly_BlocksFiveEngines()
    {
        var expected = new[] { "tesseract", "vision", "whisper", "upscaler", "translator" };

        Assert.Equal(
            expected.OrderBy(id => id).ToArray(),
            WebConversionAssembly.BlockedEngineIds.OrderBy(id => id).ToArray());
    }

    [Fact]
    public void WebConversionAssembly_DoesNotRegisterBlockedEngines()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "s2-engine-data");
        Directory.CreateDirectory(data);

        var converters = WebConversionAssembly.CreateConverters(new EngineEnvironment(data));

        Assert.DoesNotContain(converters, c => WebConversionAssembly.BlockedEngineIds.Contains(c.Id));
    }

    [Fact]
    public void PresetFactory_CreatesMinimalPresetInFixedOutputFolder()
    {
        var preset = PresetFactory.Create("png", "/workspace/out");

        Assert.Equal("png", preset.TargetFormat);
        Assert.Equal(OutputLocation.CustomFolder, preset.Output.Location);
        Assert.Equal("/workspace/out", preset.Output.CustomFolder);
        Assert.False(preset.Pdf.Ocr);
        Assert.False(preset.Media.Caption);
        Assert.False(preset.Translate.Enabled);
        Assert.False(preset.Upscale.Enabled);
        Assert.False(preset.EditsMetadata);
    }

    [Fact]
    public void PresetFactory_RejectsUnknownTarget() =>
        Assert.Throws<ArgumentException>(() => PresetFactory.Create("not-a-format", "/workspace/out"));

    [Fact]
    public void WebConversionGuard_RejectsEnabledOcr()
    {
        var preset = PresetFactory.Create("pdf", "/workspace/out");
        preset.Pdf.Ocr = true;

        Assert.Throws<InvalidOperationException>(() => WebConversionGuard.AssertSafePreset(preset));
    }

    [Fact]
    public void FormatTargetCatalog_UsesExplicitCategoryAndIdMapping()
    {
        var catalog = new FormatTargetCatalog(
            [
                new FileFormat("jpg", "JPG", FormatCategory.Image, ["jpg"]),
                new FileFormat("pdf", "PDF", FormatCategory.Pdf, ["pdf"]),
                new FileFormat("epub", "EPUB", FormatCategory.Ebook, ["epub"], Writable: true),
                new FileFormat("svg", "SVG", FormatCategory.Vector, ["svg"]),
                new FileFormat("eml", "EML", FormatCategory.Text, ["eml"], Writable: true),
            ],
            new AlwaysCanConvert());

        var byId = catalog.Build().ToDictionary(f => f.Id);

        Assert.Contains("jpg", byId["pdf"].Targets);
        Assert.Contains("pdf", byId["jpg"].Targets);
        Assert.Empty(byId["epub"].Targets);
        Assert.Empty(byId["svg"].Targets);
        Assert.Contains("eml", byId["jpg"].Targets);
    }

    private sealed class AlwaysCanConvert : ITargetPlanner
    {
        public bool CanConvert(string from, string to) => true;
    }
}
