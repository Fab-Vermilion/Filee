using Filee.Core.Presets;

namespace Filee.Web.Conversion;

/// <summary>
/// Runtime assertion gate. Even if a caller bypasses the public API shape and hands a mutated
/// Preset to the job layer, conversion must never enable the removed capabilities.
/// </summary>
public static class WebConversionGuard
{
    private const string TranslationToolId = "translate";

    public static void AssertSafePreset(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (preset.Pdf.Ocr)
            throw new InvalidOperationException("OCR is disabled for the Web application.");
        if (preset.Media.Caption)
            throw new InvalidOperationException("Speech transcription is disabled for the Web application.");
        if (preset.Translate.Enabled || string.Equals(preset.Tool, TranslationToolId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Machine translation is disabled for the Web application.");
        if (preset.Upscale.Enabled || preset.IsUpscaleTool)
            throw new InvalidOperationException("AI upscaling is disabled for the Web application.");
        if (preset.EditsMetadata)
            throw new InvalidOperationException("Metadata edits are disabled for the Web application.");
    }
}
