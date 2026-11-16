using System.Collections.Concurrent;
using Filee.Core.Conversion;

namespace Filee.Web.Jobs;

/// <summary>Maps Web job ids to internal ConversionJob ids and final outputs. Never exposed to clients.</summary>
public sealed class WebJobRegistry
{
    private readonly ConcurrentDictionary<string, ConversionJob> _byWebId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _webIdByInternalId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _outputs = new(StringComparer.Ordinal);

    public void Add(string webId, ConversionJob job)
    {
        _byWebId[webId] = job;
        _webIdByInternalId[job.Id] = webId;
    }

    public ConversionJob? Get(string webId) => _byWebId.GetValueOrDefault(webId);

    public bool TryGetWebId(string internalId, out string webId) =>
        _webIdByInternalId.TryGetValue(internalId, out webId!);

    public void SetOutput(string webId, string path) => _outputs[webId] = path;

    public bool TryGetOutput(string webId, out string path) => _outputs.TryGetValue(webId, out path!);
}
