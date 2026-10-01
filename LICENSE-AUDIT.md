# SupCraft 组件授权与二次开发核查

核查日期：2026-10-01。范围：Windows v0.6.1、macOS arm64 v0.7.1

后续处理：版权持有人已明确允许自由使用。SupCraft 自有代码现采用 0BSD，第三方鸣谢、许可原文、固定源码与修改记录见 LICENSING.md、THIRD-PARTY-NOTICES.md 和 legal/；Mac v0.7.2 增加实际运行时材料收集。下文是初次检查记录，未确认的第三方来源仍以 legal/SOURCES.md 为准。

结论：已确认的主要组件允许使用、修改、再分发和商业使用，但需要分别遵守许可证。**当前发布材料存在许可声明及对应源码提供方面的缺口，不能据此认定发行包已全面合规。**免费发布同样需要履行这些义务。此文件记录核查结果，不给 SupCraft 自身代码指定许可证，也不替代对最终发行包的专业法律审查。

## 主要组件

| 组件与当前用途 | 核实的许可 | 二次开发和分发要求 |
| --- | --- | --- |
| SUPer v0.2.8，Windows / Mac 的独立 PGS 编码器 | GPL-3.0-or-later；以源码文件头和 LICENSE 为准 | 允许收费和修改；分发编码器或修改版时，保留声明、提供 GPL 文本及完整对应源码、修改和构建脚本。Mac 的 `supercli.py`、`SUPer/utils.py` 修改也在此范围内。 |
| libimagequant，渲染与编码量化 | GPLv3 或更高版本，另有历史版权声明；也提供商业授权 | GPL 路线需要履行源码等义务；闭源集成或商店分发应核对商业许可。MIT 的 piliq 包装层不会改变底层库的许可。 |
| ass2bdnxml v0.7h / 固定源码提交 | 源码开头是 ISC 授权，cubicibo 修改声明沿用同一授权 | 保留 Martin Herkt 与 cubicibo 的版权和 ISC 许可；本项目渲染器链接 GPL 的 libimagequant，不能仅凭 ISC 源码许可把整个链接产物按宽松许可分发，应满足该 GPL 组合的要求。 |
| libass | ISC | 允许闭源和商用；保留版权、许可和免责声明；其依赖另行核对。 |
| avs2bdnxml 2.08，Windows 兼容模式 | 本地 COPYING 为 GPLv3 | 分发可执行文件仍有对应源码义务；附 COPYING 本身不够。当前未记录准确源码提交。 |
| S-VSFilterMod.dll，Windows 兼容模式 | sorayuki/VSFilterMod 上游 LICENSE 为 GPLv3；当前 DLL 的确切源码来源未确认 | 本地版本资源为 2.5.2.2，版权为 MPC-HC Team / 2001–2020。应定位该具体构建的来源、实际许可证及对应源码；不能用同名仓库代替二进制来源证明。 |
| Avalonia Community，Mac 界面 | NuGet 元数据为 MIT | 可以修改、闭源、商用；保留许可和版权。本项目未引用 Avalonia XPF 等商业产品。 |
| SkiaSharp / HarfBuzzSharp / MicroCom.Runtime | 核对的 NuGet 包为 MIT | 保留声明；原生 Skia、HarfBuzz 及嵌入的第三方代码仍需附自己的通知。不能只附包装库的 MIT 文本。 |
| .NET 自包含运行时 / Python 冻结运行时 | .NET 以 MIT 为主；Python 为 PSF 授权及历史声明 | 随程序分发时保留实际运行时发行物的许可与第三方通知。Windows 使用系统 .NET Framework，不等于 Mac 的自包含 .NET 发布。 |
| ScenariStream / piliq / brule / SSIM-PIL / PyOpenCL | 已读固定提交或发布元数据：MIT | 允许修改、商用；保留许可和版权。包装层许可不代替其调用的原生库许可。 |
| PyInstaller 6.16.0 | GPLv2-or-later + 打包例外；部分文件 Apache-2.0 | 打包例外允许商业、闭源程序，不会因使用打包器强制业务代码 GPL；被打包依赖的许可仍需满足。分发修改过的 PyInstaller 本身则遵守其许可。 |
| SupMover，源码包内的参考源码 | AGPLv3 | 当前没有接入 GUI 或分发其可执行文件，但源码副本需要保留许可；以后复制/修改/集成其代码时，应重新核对 AGPL 范围。修改后的网络服务还可能有向网络用户提供源码的义务。 |

依据：[SUPer](https://github.com/cubicibo/SUPer/blob/07217ac46cd92bb0b6c65c2889806f2fe6e53024/LICENSE)、[ass2bdnxml 源码头](https://github.com/cubicibo/ass2bdnxml/blob/ce88fd13134beb5403a3759f72a64b8779112da6/ass2bdnxml.c)、[libass](https://github.com/libass/libass/blob/master/COPYING)、[libimagequant 官方授权说明](https://pngquant.org/lib/)、[VSFilterMod 上游](https://github.com/sorayuki/VSFilterMod/blob/master/LICENSE)、[Avalonia](https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md)、[PyInstaller 6.16.0](https://pyinstaller.org/en/v6.16.0/license.html)、[SupMover](https://github.com/MonoS/SupMover/blob/master/LICENSE)。

## 实际发布包里的传递依赖

不能只检查 README 列出的四个转换工具。Mac 的 Python 编码器还带入原生库和其他 Python 包。

| 组件 | 许可与注意事项 |
| --- | --- |
| Pillow 11.3.0 | MIT-CMU；保存原许可及其嵌入库的声明。 |
| NumPy 2.2.6 / Numba 0.61.2 / llvmlite 0.44.0 | BSD 系列；轮子中的数学库、LLVM 等另有通知，不能仅保留顶层 BSD。 |
| OpenCV / opencv-python 4.12.0.88 | OpenCV 主体为 Apache-2.0，打包层及原生依赖另有许可。Apache 修改声明、LICENSE 及上游存在的 NOTICE 应保留。 |
| **本 Mac 包的 FFmpeg 7.1.1_3** | 实际二进制配置含 `--enable-gpl --enable-version3 --enable-libx264 --enable-libx265`，属于 GPLv3 构建；需提供对应源码、构建配置及所带入库的材料。不能套用“所有 OpenCV 轮子里的 FFmpeg 都是 LGPL”的结论。 |
| x264 / x265 等视频依赖 | 已在 Mac 包中发现；分别核对 GPL / 商业授权及准确版本源码。SupCraft 不进行视频编码，并不会消除随包分发这些库的义务。 |
| GLib / libintl（gettext）/ FriBidi | 库以 LGPL 为主；分发库时保留许可、提供相应源码，并保留用户替换/修改库的能力。包中代码与工具可能采用不同许可，不能给全部文件套一个 LGPL 标签。 |
| FreeType | 可按 FTL 路线处理；需要保留相应许可和署名。 |
| HarfBuzz / Graphite2 / libunibreak / PCRE2 / libpng | 分别涉及 MIT、Graphite2 的多授权选择、Zlib、BSD、libpng 许可；保留准确版本的声明。 |
| tqdm 4.67.1 | `MPL-2.0 AND MIT`，按文件范围处理；MPL 文件及其修改的源码可获得性义务不是把整个 SupCraft 都改为 MPL。 |
| siphash24 1.7 | 发布元数据列出 Apache 与 LGPL，需继续核对源文件/双授权范围；本次未将其误标为全 MIT。 |
| timecode、pytools、platformdirs、setuptools、wheel | 核对的版本主要为 MIT；部分包的内嵌第三方文件仍需单独通知。 |
| packaging / typing_extensions | 分别为 Apache-2.0 OR BSD-2-Clause，以及 PSF-2.0。 |

上表主要 Python 许可使用固定版本的 [PyPI 元数据](https://pypi.org/)核对；原生库许可辅以 [Homebrew 官方 Formula 元数据](https://formulae.brew.sh/)核对。Formula 当前版本不等于已打包二进制的准确版本，应以发行时的构建记录补齐。Windows DLL 也需要逐个建立同样的清单，包括 Fontconfig、GLib、FriBidi、libiconv、libintl、FreeType、MinGW 运行库等。

FFmpeg 二进制证据：

- 检查 ZIP：`SupCraft-macOS-0.7.1-arm64.zip`，SHA256 `e853ee061930ee48a54304d1005e05bd29a48afe3d45574e3f1ff180c3bb7ac8`。
- 读取 `SupCraft.app/Contents/Resources/tools/super/_internal/cv2/.dylibs/libavcodec.61.19.101.dylib` 的配置字符串。
- 配置前缀为 `/opt/homebrew/Cellar/ffmpeg/7.1.1_3`，上述 GPL / version3 / x264 / x265 标志实际存在。上层同名文件是符号链接，不能把链接内容当作二进制来扫描。
- [FFmpeg 官方说明](https://ffmpeg.org/legal.html)明确：开启 GPL 部分会改变整个 FFmpeg 构建的许可要求。

本次已检查主要工具、直接包及关键传递依赖；未逐一完成所有内嵌编解码器、运行库源文件的法律归属确认，不能把此表当作全量合规证书。

## 二次开发的实际要求

1. **仅在本机修改和使用**：GPL 通常不要求把私人修改公开。给他人提供程序副本（包括免费发布、收费销售和普通对外交付）时，开始核对分发义务；AGPL 的特定网络交互义务另计。
2. **MIT / ISC / BSD 类组件**：通常无需公开自己全部代码，但分发时必须保留原版权、许可和免责声明；禁止用自己的声明取代上游声明。
3. **GPL 后端**：保存准确版本的完整可修改源码、所有修改、构建/安装脚本及必要依赖材料，并按 GPL 可接受的方式提供给接收者。版本号、仓库主页、下载原始二进制的链接、几份适配文件和字节码都不能单独证明已满足完整对应源码要求。
4. **GPL 并不禁止收费**：可以销售软件、技术支持或服务；不能在 GPL 覆盖部分附加“不得商用、不得再分发、不得修改”等限制。闭源集成 GPL 代码与 GPL 下销售开源程序是不同的授权情形。
5. **LGPL / MPL**：LGPL 库的修改、对应源码与可替换/重链接能力需处理；MPL 主要按覆盖文件处理。不要把它们统称为“宽松许可证”。
6. **商业授权**：libimagequant 官方提供额外商业许可，但购买一个库的许可不会消除 SUPer、FFmpeg 等其他 GPL 组件的义务。商店上架、闭源发布或最终用户协议需单独复核。
7. **字体和素材**：目前发布包未加入测试字体或用户字幕。用户指定目录、收集字体的功能不赋予你再分发商业字体的权利；若以后预装字体，必须另查其许可。程序生成的字幕文件也不会仅因使用 GPL 工具就自动变为 GPL。

GPL 的分发条款见 [GPLv3 第 5、6 节](https://www.gnu.org/licenses/gpl-3.0.en.html)；独立程序边界见 [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation)。

## SupCraft 自身是否必须全部采用 GPL

从当前代码结构看，C# GUI 通过独立进程、命令行参数及 ASS / BDN / PNG 文件调用转换器，没有把 SUPer 的 Python 实现或 GPL 转换器代码直接链接进 GUI。**据此可以考虑独立程序的聚合分发，但这属于基于结构的判断，不是“用了子进程就必然免 GPL”的保证。**程序之间通信的内容、是否复制 GPL 代码，以及后续集成方式也会影响结论。

作为独立作品的 GUI 可以选择自己的许可；GPL 后端、链接 GPL 库的渲染器以及对 GPL 源码的修改仍需满足各自条件。若将 SUPer 算法复制进 C#、把 GPL 库直接集成 GUI，或直接集成 SupMover 的 AGPL 代码，必须重新判断授权范围。

初次检查时仓库根目录没有 SupCraft 自身的 `LICENSE`；后续已按版权持有人的明确授权添加 0BSD，仅覆盖自有代码。公开放在 GitHub 本身不自动授权二次开发和再分发；第三方依赖保持原许可。[GitHub 官方说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)。

## 当前发布材料的缺口与补齐顺序

1. **对应源码不完整**：复核确认 avs2bdnxml 目录已有 2.08 的 C / ASM 源码、头文件、Makefile 和 COPYING，原先未充分识别，应予更正。其静态依赖和其他 Windows 原生 DLL 的准确构建来源仍需核对。Mac v0.7.1 的 Resources/source 只带适配文件；后续已加入五份完整固定源码归档，但不能代替所有原生传递依赖的对应源码。
2. **许可收集不完整**：Mac 的顶层 `Resources/licenses` 只有 SUPer、ScenariStream、piliq、brule 四份。构建脚本只匹配上游根目录 `LICENSE*` / `COPYING*`，漏掉 ass2bdnxml 源码头里的 ISC，以及 libass、libimagequant、FFmpeg、.NET、Avalonia、Skia 和部分 Python 依赖的声明。冻结环境自带的少量许可不能覆盖全部依赖。
3. **二进制来源需补齐**：记录 Windows VSFilterMod 2.5.2.2（SHA256 `95fdf6d24f4b95d3eba405b1996979453c7c05b34415c52a74ee99de0c566fc6`）、avs2bdnxml 2.08 及 DLL 的准确来源、源码提交、补丁与构建方法；Mac 保存实际 Homebrew 版本、构建配置和全量依赖清单。
4. **修改声明需补齐**：为 SUPer 的按任务配置、OpenCL 主进程及工作进程回退修改记录修改内容、日期和作者，并为新增适配代码声明 GPL 兼容授权。保留原 GPL 文件头。
5. **建立发布材料**：附 `THIRD-PARTY-NOTICES`、准确版本的完整许可文本、组件清单和可获取的对应源码包；运行包下载位置明确链接源码，并确保链接持续可用。GPLv3 允许的源码提供方式有多种，不一定要求把所有源码塞进 `.app`。
6. **选定自身许可后再声明开源**：若愿意整体采用 GPLv3，这是与现有现代后端较一致的方案，但仍不能替代上述第三方材料。若希望 GUI 闭源，应保持独立程序边界，并复核所有后端和最终分发方式；单独给 GUI 加 MIT 文件不会把第三方变成 MIT。

初次打包只收集了部分许可，后续已增加授权、鸣谢、固定源码和运行时材料收集。尚未确认的二进制来源与传递依赖必须继续核对，不能因新增鸣谢而认为所有分发义务已完成。
