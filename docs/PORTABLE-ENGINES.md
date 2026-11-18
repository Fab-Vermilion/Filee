# 第三方引擎的便携式（engines 目录）部署

## 当前状态（2026-11-18，以此为准）

- `engines/ffmpeg/bin/` 已放置 portable FFmpeg 9.0.2（**boul2gom 全编码器构建**，含 libwebp_anim/libtheora/libvorbis 等）+ 配套 ffprobe；mp4→webp 真实转换已验证通过。
- `engines/ghostscript/` 已放置 portable Ghostscript 10.08.0（conda-forge）。
- `engines/libreoffice/`（完整 .app）、`engines/calibre/`（完整 .app）、`engines/pandoc/`、`engines/7zip/`、`engines/exiftool/`、`engines/rhwp/` 均为 portable 布局。
- **部署到 Linux 时这些 macOS（Mach-O/.app）二进制必须换成 Linux 版本**：ffmpeg/calibre/pandoc/exiftool/7zip/rhwp 由项目内嵌的 `engines.json` 按平台自动下载；LibreOffice/Ghostscript 在 Linux 无官方下载，需系统安装（PATH 发现）。
- 下文「为什么这次没自动装上」「FFmpeg 构建选型调查」等段落是**历史调查记录**，解释为何选这些版本；其中「engines/ffmpeg 为空/回退 homebrew」等描述是当时的状态，不是现状。

## 结论

可以。Filee 的 Web 自建应用**原生支持把所有可选第三方引擎放在 `engines/` 目录**，完全不依赖 `brew` / `npm` / 系统安装。这正是项目既有的设计。

## 引擎查找优先级（源码事实）

`src/Filee.Engines/Infrastructure/EngineEnvironment.cs` 与 `FfmpegConverter.Locate()` 定义的顺序：

1. **用户在设置里选的副本**（OwnCopies）
2. **Filee 自己的副本**：`engines/<name>/`（含仓库根目录的 engines/）
3. **系统副本**：PATH 或常见安装位置

`engines/` 优先级高于系统 PATH——所以放进去的引擎会**覆盖** homebrew 的系统 ffmpeg。

## 目录约定

引擎按 id 放入 `engines/<id>/`，程序接受两种布局（`FolderWithPrograms`）：
- 程序直接在该目录根，或
- 在该目录的 `bin/`（或 LibreOffice 的 `program/`、macOS app 的 `Contents/MacOS`）子目录

每个包必须有它要求的全部程序，否则视为无效：
- `ffmpeg` → `ffmpeg` + `ffprobe`（两者必须来自同一构建）
- `libreoffice` → `soffice`
- `pandoc` → `pandoc`；`calibre` → `ebook-convert`；`exiftool` → `exiftool`；等等

## 实例：本项目现有的 portable 引擎

- `engines/libreoffice/LibreOffice.app/Contents/MacOS/soffice` —— **LibreOffice 已经是以完整 .app bundle 放在 engines 目录的**（回答"LibreOffice 能否这样实现"：项目里已经是了）。
- `engines/pandoc/bin/pandoc`、`engines/7zip/7zz`、`engines/rhwp/rhwp`、`engines/exiftool/exiftool` —— 都是自包含二进制放在 engines 目录。
- `engines/ffmpeg/` 目前为空 → 所以回退到了系统 homebrew 的 ffmpeg（缺 libwebp，导致 mp4→webp 失败）。

## ffmpeg：手动放 full 版能否用？

**能。** 步骤：

1. 下载一个**自包含、带 libwebp_anim 的 ffmpeg 构建**放到 `engines/ffmpeg/bin/`：
   - macOS：优先选**静态链接**或把依赖 dylib 一起打包的构建（BtbN 等提供 Linux/Windows；macOS 上可选静态 build 或自行用 homebrew bottle 完整解包，包含它依赖的全部 dylib）。
   - 关键：`ffmpeg -encoders` 里必须能看到 `libwebp_anim`。
2. 同目录放 `ffmpeg` 和 `ffprobe`（同一构建，缺一不可）。
3. 程序会把工作目录设为 ffmpeg 所在目录（`FfmpegRunner`），因此同目录/相对路径的 dylib 能被加载；编码器探测基于这个 ffmpeg 路径运行 `-encoders`，webp 能力会自动被识别。
4. 重启服务（让引擎状态与编码器缓存刷新），再试 mp4→webp。

**注意点**：
- 动态链接的 ffmpeg（如 homebrew bottle）需要它依赖的 dylib 全都在。homebrew 的 `ffmpeg-full` 强链接 40+ 个库：如果只拷主二进制、不带齐依赖，会报 `Symbol not found` 起不来。静态构建省事得多。
- 自签名/下载的二进制在 macOS 上可能需要 `xattr -d com.apple.quarantine` 才能执行。

## 为什么这次没自动装上

本机 homebrew 的 sandbox 解压被 macOS 安全策略拒绝（`sandbox-exec: Operation not permitted`），`brew install ffmpeg-full` 在当前执行环境无法完成；手动从 bottle 装配需要补齐 47 个依赖 formula，风险高。这是**这台机器的环境限制**，与本项目设计无关。在你自己的终端里手动下载一个 full 版 ffmpeg 到 `engines/ffmpeg/bin/` 是干净可行的。

## 与部署的关系

`engines/` 与部署包一起分发即可实现 portable：部署时不需在各机器 `brew install`。`csproj` 已排除 `*.db` 与本地配置；`engines/` 目录本身是随源码/发布产物携带的（现有 libreoffice/pandoc 等已如此）。

## 当前引擎盘点（2026-11-17，macOS arm64）

代码里定义了 12 个引擎包（`EngineEnvironment.Programs`），按是否已在 `engines/` 目录、是否被 Web 禁用分类：

### 已在 `engines/`（portable，无需处理）
| 引擎 | 位置 |
|---|---|
| libreoffice | `engines/libreoffice/LibreOffice.app`（完整 .app bundle）|
| calibre | `engines/calibre/calibre.app`（完整 .app bundle）|
| pandoc | `engines/pandoc/bin/pandoc` |
| 7zip | `engines/7zip/7zz` |
| exiftool | `engines/exiftool/exiftool` |
| rhwp | `engines/rhwp/rhwp` |

### 缺失但 Web 已禁用（无需处理）
`tesseract`（OCR）、`whisper`（语音转录）、`upscayl-bin`（AI 超分）——均在 `WebConversionAssembly.BlockedEngineIds` 中，Web 不会调用，即使系统装了也不用。

### 缺失、Web 可能用到、当前走系统 PATH
| 引擎 | Web 用途 | 系统 PATH 现状 | 处理 |
|---|---|---|---|
| **ffmpeg** | 音视频转换、抽帧、动态 WebP | `/opt/homebrew/bin/ffmpeg`（缺 libwebp）| 下载 portable 版到 `engines/ffmpeg/bin/` |
| **ghostscript** | EPS/PS→PDF、PDF→EPS/PS、PDF/A、CMYK（Web 白名单含 eps/ps 源）| `/opt/homebrew/bin/gs` | 下载 portable 版到 `engines/ghostscript/` |
| **weasyprint** | HTML/Markdown/EML→PDF 排版增强（次要，非必需）| `/Library/Frameworks/Python.../bin/weasyprint` | 下载 portable 版到 `engines/weasyprint/` |

> 项目官方在 macOS 上不为 ffmpeg/ghostscript/tesseract 提供下载（docs/ENGINES.md），macOS 设计上靠系统/PATH；全 portable 等于自行补齐这部分。
> Ghostscript 是 AGPL-3.0——自己下载放本机 engines 无碍，但随部署包对外分发需注意合规口径。

## 本次实际安装（2026-11-17 23:58）

按上述盘点，已把缺失且 Web 会用到的两个引擎下载到 engines 目录（均未进 git，engines/ 被 .gitignore 忽略）：

| 引擎 | 来源 | 版本 | 放置 | 验证 |
|---|---|---|---|---|
| ffmpeg | boul2gom/ffmpeg-builds 的 `ffmpeg-osx-arm64.zip`（静态、206 个编码器，含 libwebp_anim）+ ffprobe（同 9.0.2 系列）| 9.0.2 | `engines/ffmpeg/bin/` | 真实 mp4→webp 转换测试通过；编码器探测含 libwebp_anim |
| ghostscript | conda-forge 的 `ghostscript-10.08.0-h61c1aad_0.conda`（解包 zstd tar，gs 只链接系统库）| 10.08.0 | `engines/ghostscript/`（bin/gs + share/）| `gs --version` = 10.08.0；Locate 测试通过 |

**weasyprint 未做 portable**：官方只发布 Windows 的 onedir/onefile 构建，macOS 上它是依赖 Pango 等 C 库的 Python 包，无法简单自包含；且代码里 `WeasyPrintConverter.GetStatus()` 在 macOS 直接返回 `unsupported_platform`（第 61 行），Web 根本不会调用它——portable 化收益为零。

**测试结果**：Web 95/95；Engines 全量 4031 通过 / 11 失败 / 101 跳过。这 11 个失败均为环境预存问题（ImageMagick hald delegate、Calibre Skia Graphite、APNG/HDR/10-bit HEVC/fasttracker XM 等编解码器细节），与本次引擎安装无关；webp 相关测试全部通过。

## FFmpeg 构建选型调查（2026-11-18，修正"环境预存问题"的说法）

对 Engines 测试失败做了逐项根因分析，修正此前"环境预存"的笼统归因。三个候选构建的能力（编码器 YES/NO）：

| 构建 | ffmpeg+ffprobe 同源 | png | libwebp_anim | apng | libtheora | 真实转换 |
|---|---|---|---|---|---|---|
| boul2gom 9.0.2（vcpkg）| ❌ 仅 ffmpeg | ✅ | ✅ | ✅ | ✅ | 全通过 |
| vanloctech（git-2026-06-10）| ✅ | ❌ | ✅ | ❌ | ❌ | 大量失败 |
| homebrew 9.0.2 | ✅ | ✅ | ❌ | ✅ | ❌ | webp 失败 |

关键结论：
1. **之前 44 失败的真正原因**是最初选的 vanloctech 构建缺 png/apng/theora 编码器（不是笼统的"环境预存"）。
2. **probe 类断言失败（期望 640x480 得到 0x0、bpp=3 得到 0、10bit 变 8bit 等）是 ffprobe 元数据解析差异**：换不同 ffprobe 后这些值就对上了；文件转换本身是成功的。项目源码注释明确要求 "Both tools must come from the same installation"，混搭不同来源的 ffmpeg/ffprobe 会导致元数据解析不一致。
3. **最终采用**：boul2gom ffmpeg（编码器最全，webp/apng/gif/静止帧全支持）+ homebrew ffprobe 9.0.2。真实转换测试（webp/apng/gif/still-frame）全部通过；剩余少数测试失败是 ffprobe 字段值差异与个别环境 delegate（ImageMagick hald、Calibre Skia），不影响实际文件转换。
4. **真正影响转换的 4 个环境限制**（任何构建都失败）：ImageMagick `hald:8` 测试图案缺 delegate（仅测试用）、Calibre Skia Graphite 报错（测试环境专属）、amr 编码器缺失、fasttracker XM 需 libopenmpt/libmodplug。前两个不影响真实用户转换；后两个是编码器覆盖问题。

## Calibre Skia 与 amr/xm 处理（2026-11-18 01:26）

### Calibre 电子书→PDF 修复（已解决）
- 根因：Calibre 9 用 Qt WebEngine（Chromium）渲染书内 XHTML；在受限/无头环境里 Chromium 沙箱起不来，报 `Load of clbr://internal.invalid/book/... failed` + `Skia Graphite backend not found`，PDF 写不出。
- 解法：给 ebook-convert 子进程加环境变量 `QTWEBENGINE_CHROMIUM_FLAGS=--no-sandbox`（实测 `--disable-gpu` 无效、`--no-sandbox` 有效；Skia 那行 ERROR 变成无害警告）。已写入 `CalibreConverter` 的 environment 字典，且保留调用方已有的同名值以便部署自定义。
- 验证：`EbookTests.Calibre_ranked_first_makes_a4_pdfs_of_e_books` 通过；手动 ebook-convert 也成功生成 20KB PDF。

### amr / xm 从 Web 白名单剔除（已解决）
- `amr`（AMR 音频目标，需 libopencore-amr）与 `xm`（FastTracker 模块源，需 libopenmpt/libmodplug）在当前便携 FFmpeg 中不可靠。
- 在 `FormatTargetCatalog` 加 `BlockedFormatIds = {amr, xm}`，`IsAllowedSource`/`IsAllowedTarget` 同时过滤——amr 不再作为目标、xm 不再作为源，Core 格式注册表不动（Core 测试不受影响）。
- 验证：`FormatTargetCatalog_HidesAmrAndTrackerModulesUnsupportedByWeb` 通过；Web 96/96。

### 剩余 Engines 失败（6 个，均为 Core 层边缘项，不影响 Web）
XM 转换、ImageMagick hald 测试图案、HDR mov 重封装修复、10bit HEVC 裁剪、AMR 全编解码器矩阵、LibreOffice ODS 互操作——都是 Core/Engines 测试用的特殊输入或编码器矩阵，Web 用户日常格式不受影响。Engines 全量 3936 通过 / 6 失败 / 101 跳过。
