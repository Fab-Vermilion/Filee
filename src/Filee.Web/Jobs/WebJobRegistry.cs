using System.Collections.Concurrent;
using Filee.Core.Conversion;

namespace Filee.Web.Jobs;

/// <summary>
/// Maps Web job ids to internal ConversionJob ids and final outputs. Never exposed to clients.
/// Outputs are kept per source file (index-aligned with job.Files) so a batch's download links each
/// resolve to that source's own converted file.
/// </summary>
public sealed class WebJobRegistry
{
    private readonly ConcurrentDictionary<string, ConversionJob> _byWebId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _webIdByInternalId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string[]> _outputs = new(StringComparer.Ordinal);

    public void Add(string webId, ConversionJob job)
    {
        _byWebId[webId] = job;
        _webIdByInternalId[job.Id] = webId;
    }

    public ConversionJob? Get(string webId) => _byWebId.GetValueOrDefault(webId);

    public bool TryGetWebId(string internalId, out string webId) =>
        _webIdByInternalId.TryGetValue(internalId, out webId!);

    public void SetOutputs(string webId, string[] outputs) => _outputs[webId] = outputs;

    /// <summary>Returns the output path aligned with a source-file index, if any.</summary>
    public bool TryGetOutput(string webId, int index, out string path)
    {
        path = string.Empty;
        if (!_outputs.TryGetValue(webId, out var outputs) || index < 0 || index >= outputs.Length)
            return false;
        path = outputs[index];
        return !string.IsNullOrEmpty(path);
    }

    public bool HasOutputs(string webId) =>
        _outputs.TryGetValue(webId, out var outputs) && outputs.Any(p => !string.IsNullOrEmpty(p));
}
