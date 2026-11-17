using Filee.Core.Conversion;

namespace Filee.Web.Conversion;

/// <summary>Adapts the cached ConverterCatalog to the narrow target-list seam.</summary>
public sealed class RoutePlannerAdapter(ConverterCatalog catalog) : ITargetPlanner
{
    // /api/formats evaluates many (source,target) pairs. Creating a RoutePlanner each time is O(N^2)
    // LINQ filtering over converters and dominated first-load latency; cache a single planner instead.
    private readonly RoutePlanner _planner = catalog.CreatePlanner();

    public bool CanConvert(string from, string to) => _planner.CanConvert(from, to);
}
