using Filee.Core.Formats;

namespace Filee.Web.Conversion;

/// <summary>Wire shape for GET /api/formats.</summary>
public sealed record FormatDescriptor(
    string Id,
    string Name,
    string Category,
    IReadOnlyList<string> Extensions,
    bool Writable,
    IReadOnlyList<string> Targets);

public sealed record FormatDescriptorCollection(IReadOnlyList<FormatDescriptor> Formats);

/// <summary>
/// Builds the whitelisted target list from v2.1 §11. Mapping uses FileFormat.Category plus explicit
/// format-id fallbacks; the source tree has no FormatRegistry.category field.
/// </summary>
public sealed class FormatTargetCatalog
{
    private static readonly HashSet<FormatCategory> SourceCategories =
    [
        FormatCategory.Image,
        FormatCategory.Pdf,
        FormatCategory.Document,
        FormatCategory.Spreadsheet,
        FormatCategory.Presentation,
        FormatCategory.Hwp,
        FormatCategory.Text,
        FormatCategory.Subtitle,
        FormatCategory.Video,
        FormatCategory.Audio,
        FormatCategory.Archive,
        FormatCategory.Data,
    ];

    private static readonly HashSet<FormatCategory> TargetCategories =
    [
        FormatCategory.Image,
        FormatCategory.Pdf,
        FormatCategory.Document,
        FormatCategory.Spreadsheet,
        FormatCategory.Presentation,
        FormatCategory.Hwp,
        FormatCategory.Text,
        FormatCategory.Subtitle,
        FormatCategory.Video,
        FormatCategory.Audio,
        FormatCategory.Archive,
        FormatCategory.Data,
    ];

    private static readonly HashSet<string> IdFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        "eml",
        "msg",
        "p7m",
    };

    // §11 allows EPS/PS -> PDF. SVG stays outside this stage even though it shares the Vector category.
    private static readonly HashSet<string> VectorSourceIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "eps",
        "ps",
    };

    private readonly IReadOnlyList<FileFormat> _formats;
    private readonly ITargetPlanner _planner;

    public FormatTargetCatalog(IEnumerable<FileFormat> formats, ITargetPlanner planner)
    {
        _formats = formats.ToArray();
        _planner = planner;
    }

    public IReadOnlyList<FormatDescriptor> Build() =>
        _formats.Select(format => new FormatDescriptor(
            format.Id,
            format.DisplayName,
            format.Category.ToString(),
            format.Extensions.ToArray(),
            format.Writable,
            TargetsFor(format))).ToArray();

    public FormatDescriptorCollection BuildCollection() => new(Build());

    private IReadOnlyList<string> TargetsFor(FileFormat source)
    {
        if (!IsAllowedSource(source))
            return [];

        return _formats
            .Where(target => !string.Equals(target.Id, source.Id, StringComparison.OrdinalIgnoreCase))
            .Where(IsAllowedTarget)
            .Select(target => target.Id)
            .Where(id => _planner.CanConvert(source.Id, id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsAllowedSource(FileFormat format) =>
        SourceCategories.Contains(format.Category)
        || IdFallbacks.Contains(format.Id)
        || VectorSourceIds.Contains(format.Id);

    private static bool IsAllowedTarget(FileFormat format) =>
        format.Writable
        && (TargetCategories.Contains(format.Category) || IdFallbacks.Contains(format.Id));
}
