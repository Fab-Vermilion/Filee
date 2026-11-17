# 「编码器缺失」102 条失败清单（复核说明）

## ⚠️ 重要纠正

这 102 条在全量矩阵报告中被自动分类为「编码器缺失」。**经复核，这个分类是错的**——它们不是真的缺少编码器：

- 当前 engines/ffmpeg（boul2gom 9.0.2）实测**具备**所需全部编码器：`libtheora`（ogv）、`libvorbis`（ogg）、`libwebp_anim`/`libwebp`（webp）。
- 用正常的带音视频输入实测：`mp4 → ogv / ogg / webp` 三种转换**全部成功**（输出 43986 / 7352 / 20878 字节）。
- 全量矩阵中这三个目标格式也各有成功记录（共 38 条 OK）。

**真实原因**：这 102 条的输入是测试工具在「链式生成种子」阶段产生的中间文件，文件名后缀与内容格式错乱（例如内容实为 3g2、文件名却是 `gen_3g2_seed.ogv`），FFmpeg 按扩展名选编码器时找不到匹配编码器，报 `Error selecting an encoder`。是**测试种子质量问题**，不是 Web 应用对用户真实文件的转换能力缺陷。

## 分布

| 目标格式 | 条数 | 相关编码器 | 实测可用性 |
|---|---|---|---|
| ogv | 58 | libtheora | ✅ 已安装且实测成功 |
| ogg | 24 | libvorbis | ✅ 已安装且实测成功 |
| webp | 20 | libwebp_anim | ✅ 已安装且实测成功 |

## 逐条清单（102 条）

| 源格式 | 目标格式 | 分类 | 实测编码器可用性 |
|---|---|---|---|
| 3g2 | ogv | 编码器缺失（误判） | libtheora ✅ |
| 3g2 | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| 3gp | ogv | 编码器缺失（误判） | libtheora ✅ |
| 3gp | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| aac | ogg | 编码器缺失（误判） | libvorbis ✅ |
| ac3 | ogg | 编码器缺失（误判） | libvorbis ✅ |
| aiff | ogg | 编码器缺失（误判） | libvorbis ✅ |
| apng | ogv | 编码器缺失（误判） | libtheora ✅ |
| au | ogg | 编码器缺失（误判） | libvorbis ✅ |
| avi | ogv | 编码器缺失（误判） | libtheora ✅ |
| avi | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| avif | ogv | 编码器缺失（误判） | libtheora ✅ |
| bmp | ogv | 编码器缺失（误判） | libtheora ✅ |
| caf | ogg | 编码器缺失（误判） | libvorbis ✅ |
| cur | ogv | 编码器缺失（误判） | libtheora ✅ |
| dds | ogv | 编码器缺失（误判） | libtheora ✅ |
| dv | ogv | 编码器缺失（误判） | libtheora ✅ |
| dv | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| eac3 | ogg | 编码器缺失（误判） | libvorbis ✅ |
| eps | ogv | 编码器缺失（误判） | libtheora ✅ |
| exr | ogv | 编码器缺失（误判） | libtheora ✅ |
| flac | ogg | 编码器缺失（误判） | libvorbis ✅ |
| flv | ogv | 编码器缺失（误判） | libtheora ✅ |
| flv | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| gif | ogv | 编码器缺失（误判） | libtheora ✅ |
| h264 | ogv | 编码器缺失（误判） | libtheora ✅ |
| h264 | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| h265 | ogv | 编码器缺失（误判） | libtheora ✅ |
| h265 | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| hdr | ogv | 编码器缺失（误判） | libtheora ✅ |
| heic | ogv | 编码器缺失（误判） | libtheora ✅ |
| icns | ogv | 编码器缺失（误判） | libtheora ✅ |
| ico | ogv | 编码器缺失（误判） | libtheora ✅ |
| jp2 | ogv | 编码器缺失（误判） | libtheora ✅ |
| jpg | ogv | 编码器缺失（误判） | libtheora ✅ |
| jxl | ogv | 编码器缺失（误判） | libtheora ✅ |
| m2ts | ogv | 编码器缺失（误判） | libtheora ✅ |
| m2ts | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| m4a | ogg | 编码器缺失（误判） | libvorbis ✅ |
| m4b | ogg | 编码器缺失（误判） | libvorbis ✅ |
| m4r | ogg | 编码器缺失（误判） | libvorbis ✅ |
| m4v | ogv | 编码器缺失（误判） | libtheora ✅ |
| m4v | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| miff | ogv | 编码器缺失（误判） | libtheora ✅ |
| mka | ogg | 编码器缺失（误判） | libvorbis ✅ |
| mkv | ogv | 编码器缺失（误判） | libtheora ✅ |
| mkv | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| mng | ogv | 编码器缺失（误判） | libtheora ✅ |
| mov | ogv | 编码器缺失（误判） | libtheora ✅ |
| mov | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| mp2 | ogg | 编码器缺失（误判） | libvorbis ✅ |
| mp3 | ogg | 编码器缺失（误判） | libvorbis ✅ |
| mp4 | ogv | 编码器缺失（误判） | libtheora ✅ |
| mp4 | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| mpg | ogv | 编码器缺失（误判） | libtheora ✅ |
| mpg | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| mxf | ogv | 编码器缺失（误判） | libtheora ✅ |
| mxf | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| ogv | ogg | 编码器缺失（误判） | libvorbis ✅ |
| ogv | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| opus | ogg | 编码器缺失（误判） | libvorbis ✅ |
| pam | ogv | 编码器缺失（误判） | libtheora ✅ |
| pcd | ogv | 编码器缺失（误判） | libtheora ✅ |
| pcx | ogv | 编码器缺失（误判） | libtheora ✅ |
| pdf | ogv | 编码器缺失（误判） | libtheora ✅ |
| pdn | ogv | 编码器缺失（误判） | libtheora ✅ |
| pfm | ogv | 编码器缺失（误判） | libtheora ✅ |
| png | ogv | 编码器缺失（误判） | libtheora ✅ |
| ppm | ogv | 编码器缺失（误判） | libtheora ✅ |
| ps | ogv | 编码器缺失（误判） | libtheora ✅ |
| psb | ogv | 编码器缺失（误判） | libtheora ✅ |
| psd | ogv | 编码器缺失（误判） | libtheora ✅ |
| qoi | ogv | 编码器缺失（误判） | libtheora ✅ |
| ras | ogv | 编码器缺失（误判） | libtheora ✅ |
| rm | ogg | 编码器缺失（误判） | libvorbis ✅ |
| rm | ogv | 编码器缺失（误判） | libtheora ✅ |
| rm | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| sgi | ogv | 编码器缺失（误判） | libtheora ✅ |
| spx | ogg | 编码器缺失（误判） | libvorbis ✅ |
| tga | ogv | 编码器缺失（误判） | libtheora ✅ |
| tiff | ogv | 编码器缺失（误判） | libtheora ✅ |
| ts | ogv | 编码器缺失（误判） | libtheora ✅ |
| ts | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| tta | ogg | 编码器缺失（误判） | libvorbis ✅ |
| vob | ogv | 编码器缺失（误判） | libtheora ✅ |
| vob | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| voc | ogg | 编码器缺失（误判） | libvorbis ✅ |
| vtf | ogv | 编码器缺失（误判） | libtheora ✅ |
| w64 | ogg | 编码器缺失（误判） | libvorbis ✅ |
| wav | ogg | 编码器缺失（误判） | libvorbis ✅ |
| wbmp | ogv | 编码器缺失（误判） | libtheora ✅ |
| weba | ogg | 编码器缺失（误判） | libvorbis ✅ |
| webm | ogv | 编码器缺失（误判） | libtheora ✅ |
| webm | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| webp | ogv | 编码器缺失（误判） | libtheora ✅ |
| wma | ogg | 编码器缺失（误判） | libvorbis ✅ |
| wmv | ogv | 编码器缺失（误判） | libtheora ✅ |
| wmv | webp | 编码器缺失（误判） | libwebp_anim ✅ |
| wv | ogg | 编码器缺失（误判） | libvorbis ✅ |
| xbm | ogv | 编码器缺失（误判） | libtheora ✅ |
| xcf | ogv | 编码器缺失（误判） | libtheora ✅ |
| xpm | ogv | 编码器缺失（误判） | libtheora ✅ |

> 原始错误详情见 `docs/matrix/full-failures.tsv` 中分类为「编码器缺失」的行。按用户要求只记录、未修复。