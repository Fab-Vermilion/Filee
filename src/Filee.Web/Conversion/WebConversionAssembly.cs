using Filee.Core.Conversion;
using Filee.Engines;
using Filee.Engines.Infrastructure;

namespace Filee.Web.Conversion;

/// <summary>
/// The single converter registry used by the Web application. Disallowed engines are removed
/// before a ConverterCatalog/RoutePlanner is built, so forbidden edges never enter the route graph.
/// </summary>
public static class WebConversionAssembly
{
    /// <summary>
    /// Engines excluded by the v2.1 scope: OCR, speech transcription, AI upscaling and machine translation.
    /// ExifTool is intentionally not listed; it remains a supported structural metadata helper.
    /// </summary>
    public static IReadOnlySet<string> BlockedEngineIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "tesseract",
        "vision",
        "whisper",
        "upscaler",
        "translator",
    };

    public static IReadOnlyList<IConverter> CreateConverters(EngineEnvironment environment) =>
        EngineRegistry.CreateAll(environment)
            .Where(converter => !BlockedEngineIds.Contains(converter.Id))
            .ToArray();
}
