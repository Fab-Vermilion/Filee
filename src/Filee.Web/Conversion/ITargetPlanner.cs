namespace Filee.Web.Conversion;

/// <summary>
/// Minimal seam used by <see cref="FormatTargetCatalog"/> to ask whether a route exists.
/// The production implementation delegates to Filee.Core's RoutePlanner; tests inject a
/// deterministic planner so the whitelist mapping can be verified in isolation.
/// </summary>
public interface ITargetPlanner
{
    bool CanConvert(string from, string to);
}
