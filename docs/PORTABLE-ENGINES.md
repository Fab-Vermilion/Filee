# 第三方引擎的便携式（engines 目录）部署

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
