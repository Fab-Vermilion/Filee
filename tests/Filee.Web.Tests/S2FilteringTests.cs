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

    [Fact]
    public void FormatTargetCatalog_ExposesVideoToAnimatedWebp()
    {
        // Real FFmpeg converter edges (no engine binary needed) + the real registry/target catalog.
        // Confirms the upstream "video -> animated WebP" capability is visible through /api/formats.
        var converters = WebConversionAssembly.CreateConverters(
            new Filee.Engines.Infrastructure.EngineEnvironment(Path.GetTempPath()));
        var catalog = new FormatTargetCatalog(
            Filee.Core.Formats.FormatRegistry.Known,
            new RoutePlannerAdapter(new Filee.Core.Conversion.ConverterCatalog(converters)));

        var mp4 = catalog.Build().First(f => f.Id == "mp4");
        Assert.Contains("webp", mp4.Targets);
    }

    [Fact]
    public void FormatTargetCatalog_HidesAmrAndTrackerModulesUnsupportedByWeb()
    {
        var converters = WebConversionAssembly.CreateConverters(
            new Filee.Engines.Infrastructure.EngineEnvironment(Path.GetTempPath()));
        var catalog = new FormatTargetCatalog(
            Filee.Core.Formats.FormatRegistry.Known,
            new RoutePlannerAdapter(new Filee.Core.Conversion.ConverterCatalog(converters)));

        var formats = catalog.Build();
        // amr must never be offered as a target (AMR encoder absent from the portable FFmpeg).
        Assert.DoesNotContain(formats, f => f.Targets.Contains("amr", StringComparer.OrdinalIgnoreCase));
        // xm (FastTracker) must not be offered as a source (needs libopenmpt/libmodplug).
        var xm = formats.FirstOrDefault(f => f.Id == "xm");
        if (xm is not null)
            Assert.Empty(xm.Targets);
    }

    [Fact]
    public void FormatTargetCatalog_HidesEmlToZipEdgeWithoutRoute()
    {
        // The full conversion matrix found eml->zip is exposed by the whitelist but has no engine
        // route (no_route at runtime). It must not be offered to users.
        var converters = WebConversionAssembly.CreateConverters(
            new Filee.Engines.Infrastructure.EngineEnvironment(Path.GetTempPath()));
        var catalog = new FormatTargetCatalog(
            Filee.Core.Formats.FormatRegistry.Known,
            new RoutePlannerAdapter(new Filee.Core.Conversion.ConverterCatalog(converters)));

        var eml = catalog.Build().FirstOrDefault(f => f.Id == "eml");
        Assert.NotNull(eml);
        Assert.DoesNotContain("zip", eml!.Targets, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class AlwaysCanConvert : ITargetPlanner
    {
        public bool CanConvert(string from, string to) => true;
    }
}
