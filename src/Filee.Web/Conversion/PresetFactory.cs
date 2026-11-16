using Filee.Core.Formats;
using Filee.Core.Presets;

namespace Filee.Web.Conversion;

/// <summary>
/// Creates the only Preset shape the Web API accepts: a known writable target format, a fixed
/// output folder and all content-changing switches left at their defaults (false).
/// </summary>
public static class PresetFactory
{
    public static Preset Create(string target, string outputFolder)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Target format is required.", nameof(target));

        var id = target.Trim().ToLowerInvariant();
        var format = FormatRegistry.FindById(id);
        if (format is null || !format.Writable)
            throw new ArgumentException($"Unknown or read-only target format: {target}", nameof(target));

        return new Preset
        {
            TargetFormat = id,
            Output = new OutputRule
            {
                Location = OutputLocation.CustomFolder,
                CustomFolder = outputFolder,
            },
        };
    }
}
