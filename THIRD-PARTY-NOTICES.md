# 第三方组件与鸣谢

感谢以下项目的作者、贡献者与维护者，使 SupCraft 的字幕渲染、PGS 编码和桌面界面成为可能。以下组件保留原作者版权与许可证，不属于 SupCraft 自有代码的 0BSD 授权范围。

| 组件 / 作者或维护者 | 用途 | 许可与材料 |
| --- | --- | --- |
| [avs2bdnxml](https://github.com/koroban/avs2bdnxml)，Arne Bochem 及后续贡献者 | Windows AviSynth 路线 BDN / SUP 写出 | GPLv3-or-later；[源码与 Makefile](third_party/avs2bdnxml/)，[COPYING](legal/licenses/avs2bdnxml-GPL-3.0.txt) |
| [VSFilterMod](https://github.com/sorayuki/VSFilterMod)，MPC-HC Team 与 VSFilterMod 贡献者 | Windows 兼容模式 ASS 渲染 | 上游 GPLv3；[许可参考](legal/licenses/VSFilterMod-upstream-GPL-3.0.txt)。当前 DLL 2.5.2.2 的准确构建来源待确认，详见源码说明 |
| [ass2bdnxml](https://github.com/cubicibo/ass2bdnxml)，Martin Herkt（mia-0）与 cubicibo | libass 路线 BDN / PNG 输出 | [ISC 版权与许可](legal/licenses/ass2bdnxml-ISC.txt)；链接 libimagequant 的构建同时满足 GPL 要求 |
| [libass](https://github.com/libass/libass)，libass contributors | ASS 字幕渲染 | [ISC](legal/licenses/libass-ISC.txt) |
| [SUPer](https://github.com/cubicibo/SUPer)，cibo / cubicibo | PGS 编码和效果优化 | [GPLv3-or-later](legal/licenses/SUPer-GPL-3.0.txt)，包含配置读取与 OpenCL 回退适配，修改说明见 legal/MODIFICATIONS.md |
| [libimagequant](https://pngquant.org/lib/)，Kornel Lesiński 与贡献者 | 图像量化 | [GPLv3 / 历史版权声明](legal/licenses/libimagequant-COPYRIGHT.txt)，上游另提供商业许可 |
| [ScenariStream](https://github.com/cubicibo/ScenariStream)、[piliq](https://github.com/cubicibo/piliq)、[brule](https://github.com/cubicibo/brule) | 编码辅助、量化接口与 RLE | 各项目 MIT；完整原始文本位于 legal/licenses/ |
| [Avalonia Community](https://github.com/AvaloniaUI/Avalonia)，AvaloniaUI 贡献者 | Mac 桌面界面 | [MIT](legal/licenses/Avalonia-MIT.txt)；未使用 XPF 等商业组件 |
| [SkiaSharp / HarfBuzzSharp](https://github.com/mono/SkiaSharp) 与原生 Skia / HarfBuzz | 图像缩放和界面文字 | 包许可与原生代码通知见 legal/licenses/nuget/；实际打包时按 NuGet 解析版本收集 |
| [.NET](https://github.com/dotnet/runtime) 与 [Python](https://www.python.org/) | 自包含运行环境 | 各自运行时许可和第三方通知；Mac 打包时收集到应用 Resources/legal/ |
| [Pillow](https://python-pillow.org/)、[NumPy](https://numpy.org/)、[Numba](https://numba.pydata.org/)、[llvmlite](https://github.com/numba/llvmlite)、[OpenCV](https://opencv.org/) | 图像和数值处理 | MIT-CMU、BSD、Apache 等；嵌入库另有许可，按实际安装发行物收集 |
| [PyOpenCL](https://github.com/inducer/pyopencl)、[SSIM-PIL](https://github.com/ChsHub/SSIM-PIL)、[timecode](https://github.com/eoyilmaz/timecode)、[tqdm](https://github.com/tqdm/tqdm) 等 | GPU 相似度计算、时间码与进度 | MIT、MPL 等；逐包原始许可与元数据保存于发布材料 |
| [FFmpeg](https://ffmpeg.org/)，FFmpeg developers，以及 x264 / x265 等维护者 | OpenCV 间接带入的原生依赖 | v0.7.1 Mac 实际构建启用 GPL 与 version3；[GPLv3 文本](legal/licenses/GPL-3.0.txt)、准确配置及来源见 legal/SOURCES.md |
| GLib、FriBidi、gettext、FreeType、Fontconfig、HarfBuzz、Graphite2、libpng、PCRE2、libunibreak、zlib 等 | 渲染和原生依赖 | 各自 ISC / MIT / LGPL / FTL / BSD / Zlib 等；库的声明和对应源码条件独立适用 |
| [PyInstaller](https://pyinstaller.org/) 与 hooks 维护者 | 冻结编码器 | GPL 打包例外；[原许可](legal/licenses/PyInstaller-COPYING.txt)，不免除被打包依赖的义务 |
| [SupMover](https://github.com/MonoS/SupMover)，MonoS | 仅保留参考源码，尚未接入转换 | [AGPLv3](legal/licenses/SupMover-AGPL-3.0.txt)；原源码和许可保持不变 |

完整组件索引与源码获取信息见 [legal/SOURCES.md](legal/SOURCES.md)。鸣谢用于说明贡献和来源，不能代替许可原文与相应源码材料；不表示第三方作者为 SupCraft 提供背书。
