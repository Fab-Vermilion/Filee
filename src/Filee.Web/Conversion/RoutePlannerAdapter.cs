using Filee.Core.Conversion;

namespace Filee.Web.Conversion;

/// <summary>Adapts the cached ConverterCatalog to the narrow target-list seam.</summary>
public sealed class RoutePlannerAdapter(ConverterCatalog catalog) : ITargetPlanner
{
    public bool CanConvert(string from, string to) => catalog.CreatePlanner().CanConvert(from, to);
}
