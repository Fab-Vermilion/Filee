using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Web.Conversion;
using Filee.Web.Security;
using Filee.Web.Streaming;
using Microsoft.AspNetCore.Http;

namespace Filee.Web.Jobs;

/// <summary>
/// Creates one Web job from an uploaded multipart batch. The runner is injected so tests can avoid starting
/// real engines; production passes JobQueue.RunAsync through the shared concurrency gate.
/// </summary>
public sealed class JobSubmissionService(
    WebStoragePaths storage,
    JobStore store,
    WebJobRegistry registry,
    ProgressHub hub,
    WebConcurrencyGate gate,
    Func<ConversionJob, Task> runner,
    UploadLimits? defaultLimits = null)
{
    public WebStoragePaths Storage { get; } = storage;

    // ZIP / EPUB / ODT / DOCX ... all share the "PK\x03\x04" signature. Only those are handled by
    // ArchiveGuard (it opens a ZipArchive); 7z/rar/tar are validated by the engine's own guard.
    private static bool IsZip(Stream stream)
    {
        if (!stream.CanSeek) return false;
        var pos = stream.Position;
        var header = new byte[4];
        var read = stream.Read(header, 0, header.Length);
        stream.Position = pos;
        return read == 4 && header[0] == 0x50 && header[1] == 0x4B
            && header[2] == 0x03 && header[3] == 0x04;
    }

    public async Task<JobRecord> SubmitAsync(
        SessionIdentity identity,
        string? target,
        IFormFileCollection files,
        bool hasOptions,
        UploadLimits? limits = null)
    {
        limits ??= defaultLimits ?? new UploadLimits();
        if (hasOptions)
            throw new UploadException("unsupported_option");
        if (files.Count == 0)
            throw new UploadException("bad_magic");

        var webId = WebJobId.New();
        var uploadDir = Storage.UploadDirectory(webId);
        var outputDir = Storage.OutputDirectory(webId);
        Directory.CreateDirectory(uploadDir);
        Directory.CreateDirectory(outputDir);

        Preset preset;
        try
        {
            preset = PresetFactory.Create(target ?? "", outputDir);
            WebConversionGuard.AssertSafePreset(preset);
        }
        catch (ArgumentException)
        {
            throw new UploadException("invalid_target");
        }

        var sources = new List<string>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var name = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(name))
                name = $"file{index + 1}";

            await using var input = file.OpenReadStream();
            UploadGuard.Validate(name, input, limits);
            input.Position = 0;

            // ZIP-based uploads get a metadata-only bomb/traversal check (no entry is expanded).
            if (IsZip(input))
            {
                input.Position = 0;
                try { ArchiveGuard.Validate(input); }
                catch (InvalidOperationException)
                {
                    throw new UploadException("bad_magic");
                }
            }
            input.Position = 0;

            // Unique on-disk name: two different files can share a display name; never overwrite.
            var savedName = index == 0 ? name
                : $"{Path.GetFileNameWithoutExtension(name)}_{index + 1}{Path.GetExtension(name)}";
            var savedPath = Path.Combine(uploadDir, savedName);
            await using (var output = File.Create(savedPath))
                await input.CopyToAsync(output);
            sources.Add(savedPath);
        }

        var job = new ConversionJob(sources, preset, preset.TargetFormat);
        registry.Add(webId, job);
        var record = new JobRecord(
            webId, job.Id, identity.Tid, identity.Sub, preset.TargetFormat,
            "queued", 0, DateTimeOffset.UtcNow, null);
        store.Upsert(record);
        hub.Publish(webId, "queued", 0, force: true);

        _ = Task.Run(async () =>
        {
            using var lease = await gate.AcquireAsync();
            try
            {
                await runner(job);
            }
            catch
            {
                store.UpdateSummary(webId, "failed", 1, DateTimeOffset.UtcNow);
                hub.Publish(webId, "failed", 1, force: true);
            }
        });

        return record;
    }
}
