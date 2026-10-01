# 第三方源码与发行材料

本项目自有源码：[SupCraft](https://github.com/Enfana5780/SupCraft)。本目录的材料和许可不替代第三方许可证要求的完整对应源码。

## 随仓库及源码包提供的源码

- `third_party/avs2bdnxml/` 实际包含版本 2.08 的 C / ASM 源码、头文件、Makefile、COPYING 及静态依赖库。源码头保留 Arne Bochem 版权。第三方静态库的准确构建版本仍需确认，不能仅凭已有 .a 文件认为依赖源码齐全。
- `third_party/SupMover/` 为原始参考源码与 AGPLv3 文本，未接入运行程序。
- `legal/upstream-sources/` 保存用于本项目的完整上游源码归档及 SHA256；其获取 URL 和提交保存在 `legal/source-index.json`。这些是源码，不包含用户字幕或测试字体。
- Mac 上游源码归档集中放在 GitHub 的 `legal/upstream-sources/`；应用内部 `Resources/legal/SOURCE-LOCATION.txt` 指向仓库，`Resources/source/` 保留实际修改后的 SUPer 文件。`macos/build_macos.py`、`bundle_dylibs.py` 和 `runtime/` 描述构建及适配方法。

固定的上游源码位置：

| 项目 | 源码提交 |
| --- | --- |
| ass2bdnxml | [ce88fd13134beb5403a3759f72a64b8779112da6](https://github.com/cubicibo/ass2bdnxml/tree/ce88fd13134beb5403a3759f72a64b8779112da6) |
| SUPer | [07217ac46cd92bb0b6c65c2889806f2fe6e53024](https://github.com/cubicibo/SUPer/tree/07217ac46cd92bb0b6c65c2889806f2fe6e53024) |
| ScenariStream | [ed60a11a714728e6e55913b8e9a376ac2fc50288](https://github.com/cubicibo/ScenariStream/tree/ed60a11a714728e6e55913b8e9a376ac2fc50288) |
| piliq | [24c41d1cefc980415ef7aa0be90efa6de3c314c0](https://github.com/cubicibo/piliq/tree/24c41d1cefc980415ef7aa0be90efa6de3c314c0) |
| brule | [2f67b85f8deb614aad6100410b47f3df5a16c42c](https://github.com/cubicibo/brule/tree/2f67b85f8deb614aad6100410b47f3df5a16c42c) |

## 运行时与传递依赖

Mac 构建会生成 `Resources/legal/runtime-materials.json`，记录实际安装的 Python / NuGet 包和本机依赖版本、来源及收集到的许可文本。README、鸣谢、审计说明及源码材料集中放在 GitHub，不再附独立 legal ZIP。PyPI / NuGet 下载链接、上游版本链接和 Homebrew 原始发行记录用于定位源码，不能与项目自有代码许可混淆。

v0.7.1 原生包的 FFmpeg 配置字符串来自 `cv2/.dylibs/libavcodec.61.19.101.dylib`：Homebrew `ffmpeg/7.1.1_3`，启用 `--enable-gpl --enable-version3 --enable-libx264 --enable-libx265`。FFmpeg、x264、x265 和其余编解码库分别需要对应源码与构建材料。新构建收集实际 Python 包许可及 Homebrew 依赖记录，不以 README 的顶层包名代替所有原生库。

## 仍需确认的材料

1. Windows `S-VSFilterMod.dll` 的版本资源为 2.5.2.2，SHA256 `95fdf6d24f4b95d3eba405b1996979453c7c05b34415c52a74ee99de0c566fc6`。当前仓库没有该具体二进制的源码提交和补丁记录。保存的 sorayuki 上游 GPL 文本是参考，不能冒充该二进制来源已获证明。
2. Windows 原生 DLL 和 avs2bdnxml 静态依赖的准确构建版本、源码与必要补丁，需要进一步从原始发行方核对。
3. 历史 Mac v0.7.1 中 OpenCV 间接带入的 FFmpeg / x264 / x265 等，需要按实际二进制补齐对应源码及构建配置，而非下载今天的最新源码替代。
4. 包元数据和上游主页不是“对应源码已完整提供”的证明。GPL / LGPL 所需的完整材料包括适用的依赖源码、修改、构建/安装脚本；仅提供几份文本不能据此宣称全面合规。

现有材料允许核对已知来源；未确认部分保持明确标记，不作未经验证的合规或商业授权承诺。若对外分发完整第三方运行包，应先完成相关核对。
