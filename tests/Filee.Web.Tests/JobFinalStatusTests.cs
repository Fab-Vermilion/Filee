using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Web.Jobs;

namespace Filee.Web.Tests;

public sealed class JobFinalStatusTests
{
    private static ConversionJob NewJob(params string[] sources) =>
        new(sources, new Preset { TargetFormat = "pdf" }, "pdf");

    [Fact]
    public void Resolve_UsesEngineStateWhenAlreadyTerminal()
    {
        var job = NewJob("a.txt");
        typeof(ConversionJob).GetProperty("State")!.SetValue(job, JobState.Failed);

        Assert.Equal("failed", JobFinalStatus.Resolve(job));
    }

    [Fact]
    public void Resolve_KeepsRunningWhenFilesNotDone()
    {
        var job = NewJob("a.txt", "b.txt");
        typeof(ConversionJob).GetProperty("State")!.SetValue(job, JobState.Running);
        job.Files[0].State = FileState.Done;
        job.Files[0].Progress = 1;
        job.Files[1].State = FileState.Running; // still converting

        Assert.Equal("running", JobFinalStatus.Resolve(job));
    }

    [Fact]
    public void Resolve_FallsBackToCompletedWhenAllFilesDoneAndProgressFull()
    {
        var job = NewJob("a.txt", "b.txt");
        typeof(ConversionJob).GetProperty("State")!.SetValue(job, JobState.Running);
        job.Files[0].State = FileState.Done;
        job.Files[0].Progress = 1;
        job.Files[1].State = FileState.Skipped;
        job.Files[1].Progress = 1;

        Assert.Equal("completed", JobFinalStatus.Resolve(job));
    }

    [Fact]
    public void Resolve_FallsBackToCompletedWithErrorsWhenSomeFailed()
    {
        var job = NewJob("a.txt", "b.txt");
        typeof(ConversionJob).GetProperty("State")!.SetValue(job, JobState.Running);
        job.Files[0].State = FileState.Done;
        job.Files[0].Progress = 1;
        job.Files[1].State = FileState.Failed;
        job.Files[1].Progress = 1;

        Assert.Equal("completed_with_errors", JobFinalStatus.Resolve(job));
    }
}
