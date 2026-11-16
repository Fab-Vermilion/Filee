using Filee.Web.Conversion;

namespace Filee.Web.Tests;

public sealed class S9FrontendTests
{
    private static readonly string WwwRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Filee.Web", "wwwroot"));

    [Fact]
    public void DownloadDisplay_UsesOutputFileNameWhenAvailable()
    {
        var name = DownloadDisplay.Name("photo.jpg", "png", "/out/job/photo.png");

        Assert.Equal("photo.png", name);
    }

    [Fact]
    public void DownloadDisplay_FallsBackToSourceNameWithTargetExtension()
    {
        var name = DownloadDisplay.Name("report.docx", "pdf", "");

        Assert.Equal("report.pdf", name);
    }

    [Fact]
    public void Frontend_FormatsAreGroupedByCategory()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("category", js, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"groupBy|grouped|\.category", js);
        Assert.Contains("renderDonut", js);
    }

    [Fact]
    public void Frontend_HasDonutSvgWithClickSelection()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Contains("renderDonut", js);
        Assert.Contains("addEventListener", js); // 扇区点击
        Assert.Contains("donut", html, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"--surface|--text-primary|prefers-color-scheme", css);
    }

    [Theory]
    [InlineData("OCR")]
    [InlineData("翻译")]
    [InlineData("超分")]
    [InlineData("转录")]
    [InlineData("AI 读图")]
    public void Frontend_HasNoForbiddenFeatureWords(string forbidden)
    {
        var all = string.Concat(
            File.ReadAllText(Path.Combine(WwwRoot, "index.html")),
            File.ReadAllText(Path.Combine(WwwRoot, "app.js")),
            File.ReadAllText(Path.Combine(WwwRoot, "styles.css")));

        Assert.DoesNotContain(forbidden, all, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frontend_HasNoExternalCdnDependency()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var all = html + css + js;

        Assert.DoesNotContain("cdn.jsdelivr", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", all.Replace("https://127.0.0.1", ""), StringComparison.OrdinalIgnoreCase);
    }
}
