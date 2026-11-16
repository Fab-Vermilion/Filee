using System.Text.Json;
using Filee.Core.Conversion;
using Filee.Web.Contracts;

namespace Filee.Web.Tests;

public sealed class ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void ApiError_SerializesFixedEnvelope()
    {
        var json = JsonSerializer.Serialize(ApiErrorEnvelope.Create("bad_magic", "文件内容与扩展名不符"), JsonOptions);

        using var document = JsonDocument.Parse(json);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("bad_magic", error.GetProperty("code").GetString());
        Assert.Equal("文件内容与扩展名不符", error.GetProperty("message").GetString());
        Assert.False(document.RootElement.TryGetProperty("Error", out _));
    }

    [Theory]
    [InlineData(JobState.Queued, "queued")]
    [InlineData(JobState.Running, "running")]
    [InlineData(JobState.Completed, "completed")]
    [InlineData(JobState.CompletedWithErrors, "completed_with_errors")]
    [InlineData(JobState.Failed, "failed")]
    [InlineData(JobState.Cancelled, "cancelled")]
    public void JobStatusMapper_UsesContractStrings(JobState state, string expected) =>
        Assert.Equal(expected, JobStatusMapper.ToApi(state));

    [Fact]
    public void JobStatusMapper_ExposesWebInterruptedState() =>
        Assert.Equal("interrupted", JobStatusMapper.Interrupted);
}
