
## ⚠️ 最终复核结论（覆盖上文失败分类）

上文的 1190 条失败经逐层复核（用正确种子 + 指向项目 engines/ 的正确引擎），**真正的转换能力失败只有 1 条：`eml → zip`（无路由）**。其余 1189 条均为测试工具自身造成（测试种子内容/后缀问题、复核工具误用系统 FFmpeg 缺编码器、ics 测试数据缺日历字段）。详见 `TRUE-FAILURES.md`。

# Web 全格式真实转换矩阵测试报告

## 测试方法
- 对象：Web `/api/formats` 暴露的全部转换边（用真实引擎目录 engines/ 的 FFmpeg 9.0.2 / Ghostscript / LibreOffice / Calibre / pandoc 等）。
- 输入：为每个源格式造最小文件——ImageMagick 写图片、FFmpeg 从种子生成音视频、内置 writer 生成文档/数据、手写纯文本种子（csv/json/yaml/xml/srt/vtt 等），共 4 轮补齐。
- 执行：对每条边调用真实 JobQueue 跑完整转换，记录 OK/FAIL/SKIP；每条边取多轮中的最后一次结果。
- 只记录，不修复任何失败。

## 总结

| 结果 | 条数 |
|---|---|
| 成功 | 4885 |
| 失败 | 1190 |
| 无输入样本（造不出合法最小文件，未测） | 1898 |
| **合计** | **7973** |

## 失败分类

| 类别 | 条数 | 说明 |
|---|---|---|
| 语义失败（种子内容不足） | 771 | 如图片生成的视频“无音轨”、种子“无字幕/无文字/无表格”。**非能力问题** |
| FFmpeg 无输出 | 136 | 原始视频种子用 ffmpeg 裸流转封装，部分边界不产生输出 |
| ~~编码器缺失~~（复核为误判）| 102 | 原分类为编码器缺失；**经复核是测试种子后缀错乱**——实测 libtheora/libvorbis/libwebp_anim 均已安装且转换成功。详见 `ENCODER-MISSING.md` |
| 不支持/无路由 | 83 | 主要是 wtv 种子文件生成不完整，FFmpeg 无法读取 |
| 引擎报错 | 62 | FFmpeg exit 8/69/234 等，多为坏种子或解码错误率 |
| 其它 | 36 | csv→ics 无日历事件、文档无表格等语义约束 |

> 注：失败中相当比例源于“最小种子文件内容过于简单”或“种子命名/扩展名不匹配”，并非 Web 应用对用户真实文件的转换能力缺陷。完整逐条清单见 `docs/matrix/full-failures.tsv`（列：SOURCE/TARGET/CATEGORY/DETAIL）。

## 按目标格式聚合的失败数（TOP 30）

| 目标格式 | 失败数 | 源格式（示例） |
|---|---|---|
| txt | 65 | 3g2, 3gp, apng, avi, avif, bmp, cur, dds |
| ogv | 59 | 3g2, 3gp, apng, avi, avif, bmp, cur, dds |
| ogg | 46 | 3g2, 3gp, aac, ac3, aiff, au, avi, caf |
| ass | 25 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| srt | 25 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| vtt | 25 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| aac | 24 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| m4a | 24 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| m4b | 24 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| m4r | 24 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| wv | 23 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| ac3 | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| aiff | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| au | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| caf | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| eac3 | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| flac | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| mka | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| mp2 | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| mp3 | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| opus | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| smi | 22 | 3g2, 3gp, avi, dv, flv, h264, h265, lrc |
| sub | 22 | 3g2, 3gp, avi, dv, flv, h264, h265, lrc |
| tta | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| ttml | 22 | 3g2, 3gp, avi, dv, flv, h264, h265, lrc |
| voc | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| w64 | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| wav | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| weba | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |
| wma | 22 | 3g2, 3gp, avi, dv, eps, flv, h264, h265 |

## 无输入样本的源格式（39 个，未测）
ani, ape, bik, cavs, dav, dcm, dff, dsf, dss, dts, exe, fits, flc, folder, hwp, it, kra, krc, livp, m4s, mod, mpc, msg, nov, ods, ofd, p7m, qta, raw, s3m, scc, swf, tak, usm, xls, xlsm, xlsx, xlt, xltx
