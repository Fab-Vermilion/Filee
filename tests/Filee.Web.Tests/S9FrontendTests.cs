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
    public void Frontend_UsesOnlyGridForTargets_NoDonutSvg()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Contains("target-grid", js);
        Assert.Contains("target-grid", css);
        Assert.DoesNotContain("createElementNS", js);      // 不再画 SVG 甜甜圈
        Assert.DoesNotContain("class=\"seg\"", js);
        Assert.Contains("container-type: inline-size", css); // 按容器自适应
        Assert.Contains("repeat(auto-fill", css);            // 方块不拉伸铺满
        Assert.Contains("clamp(10px, 2.5cqi, 20px)", css);   // 加大间距
        Assert.Contains("text-overflow: ellipsis", css);     // 长名字不溢出方块
        Assert.Contains("0.95rem", css);                     // 字号上限随方块缩小
        Assert.Contains("aria-pressed", js);                 // 选中态可访问
        Assert.Contains(":focus-visible", css);
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

    [Fact]
    public void Frontend_ManualIsCompleteNotPlaceholder()
    {
        var manual = File.ReadAllText(Path.Combine(WwwRoot, "manual.html"));

        // all sections present
        foreach (var section in new[] { "功能介绍", "可转换类型", "使用方法", "任务与下载",
                     "隐私与数据存储", "常见问题", "第三方协议", "关于" })
            Assert.Contains(section, manual);

        Assert.DoesNotContain("占位", manual);
        Assert.DoesNotContain("placeholder", manual);
        // real content markers
        Assert.Contains("24 小时", manual);
        Assert.Contains("全部下载", manual);
        Assert.Contains("历史记录", manual);
        Assert.Contains("LibreOffice", manual);
        Assert.Contains("AGPL", manual);
        Assert.Contains("<table", manual);
        Assert.Contains("<details", manual);
        // No external *resources* (CDN scripts/styles). Plain hyperlinks to the source repo are fine.
        Assert.DoesNotContain("src=\"http", manual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import url(http", manual, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 用户实测 2026-11-17 16:43 五项改动 ----

    [Fact]
    public void Frontend_DropzoneSaysClickOnly_AndMentionsBatchLimit()
    {
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));

        Assert.Contains("点击选择文件", html);
        Assert.DoesNotContain("拖入文件", html);
        Assert.Contains("最多 20 个文件", html);
    }

    [Fact]
    public void Frontend_HasNoDropListeners()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.DoesNotContain("dataTransfer", js);   // 拖放无效，移除以免触发打开文件对话框
        Assert.DoesNotContain("dragover", js);
    }

    [Fact]
    public void Frontend_ClearsFileListOnSubmit_KeepsProgressHistory()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("state.files = []", js);              // 显式清空（onFilesChanged 是追加语义）
        Assert.Contains("renderFileList()", js);
        Assert.DoesNotContain("onFilesChanged([])", js);      // 不能用追加空数组来"清空"
        Assert.DoesNotContain("state.jobs.clear()", js);      // 「3 任务进度」不清空，保留到 TTL
    }

    [Fact]
    public void Frontend_UsesChineseStatusLabels_NoRawJobId()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("进行中", js);
        Assert.Contains("已完成", js);
        Assert.Contains("排队中", js);
        Assert.Contains("completed_with_errors", js);
        Assert.Contains("function resolvedStatus", js);
        Assert.Contains("FILE_TERMINAL", js);         // 按文件级终态兜底，不看 progress
        Assert.Contains("spinner", js);               // 进行中转圈
        Assert.DoesNotContain("jobId.slice", js);    // 任务行不再显示内部 ID
    }

    [Fact]
    public void Frontend_DownloadAllOnlyForBatch_OnStatusRow()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("filter(f => f.downloadUrl).length > 1", js); // 只在多个下载文件时出现
        Assert.Contains("全部下载", js);
        Assert.Contains("links.appendChild(Object.assign", js); // zip 按钮 append 到链接容器
        Assert.Contains("archive-link", js);
    }

    [Fact]
    public void Frontend_RestoresJobsAndShowsStorageNote()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Contains("function restoreJobs", js);
        Assert.Contains("fetch(\"/api/jobs\"", js);
        Assert.DoesNotContain("pollRestoredJob", js); // 历史任务只读，不轮询
        Assert.Contains("storage-note", html);
        Assert.Contains("storage-note", css);
        Assert.Contains("24 小时", html);
        Assert.DoesNotContain("1 小时", html);
    }

    [Fact]
    public void Frontend_ShowsFriendlyConnectionTextNotRawSub()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));

        Assert.Contains("已连接 WorkBuddy 账号", js);
        Assert.DoesNotContain("已连接：${session.sub}", js);
    }

    [Fact]
    public void Frontend_SplitsCurrentAndHistoryTabs()
    {
        var js = File.ReadAllText(Path.Combine(WwwRoot, "app.js"));
        var html = File.ReadAllText(Path.Combine(WwwRoot, "index.html"));
        var css = File.ReadAllText(Path.Combine(WwwRoot, "styles.css"));

        Assert.Contains("tab-current", html);
        Assert.Contains("tab-history", html);
        Assert.Contains("当前任务", html);
        Assert.Contains("历史记录", html);
        Assert.Contains("origin: \"session\"", js);
        Assert.Contains("origin: \"history\"", js);
        Assert.Contains("activeTab", js);
        Assert.Contains(".job-tab", css);
    }
}
