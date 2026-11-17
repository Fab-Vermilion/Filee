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

    // ---- v2 流程：先文件后格式、文件清单、百分比宽度、交集过滤 ----

    [Fact]
    public void Frontend_HasFileListContainer()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("file-list", html);
        Assert.Contains("renderFileList", js);
        Assert.Contains("size", js, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frontend_FiltersTargetsByIntersectionOfSelectedFiles()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Matches(@"intersect|intersection|交集", js);
        Assert.Contains(".targets", js);
        Assert.Contains("extensions", js, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frontend_UsesPercentWidthNotFixedMaxWidth()
    {
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Contains("width: 100%", css);
        Assert.DoesNotContain("max-width: 860px", css);
    }

    [Fact]
    public void Frontend_FileSelectionPrecedesFormatSelection()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var filePos = html.IndexOf("添加文件", StringComparison.Ordinal);
        var formatPos = html.IndexOf("选择目标格式", StringComparison.Ordinal);

        Assert.True(filePos >= 0 && formatPos >= 0);
        Assert.True(filePos < formatPos, "文件选择区域应位于目标格式选择之前");
    }

    [Fact]
    public void Frontend_FallsBackToGridWhenTooManyTargetsForDonut()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Matches(@"DONUT_MAX|MAX_DONUT|donutMax", js); // 有数量阈值
        Assert.Contains("target-grid", js);   // 超限改用网格
        Assert.Contains("target-grid", css);
    }

    [Fact]
    public void Frontend_SupportsAddMoreRemoveAndBatchLimit()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));

        Assert.Contains("MAX_FILES", js);
        Assert.Contains("function removeFile", js);
        Assert.Contains("add-more", html);
        Assert.Contains("file-remove", js);
    }

    [Fact]
    public void Frontend_HasManualPageLinkedFromHome()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var manual = File.ReadAllText(Path.Combine(WwwRoot, "manual.html"));

        Assert.Contains("/manual.html", html);
        Assert.Contains("使用手册", html);
        Assert.Contains("可转换类型", manual);
        Assert.Contains("第三方协议", manual);
        Assert.Contains("返回", manual);
    }
}
