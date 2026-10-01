# SupCraft for macOS

macOS 版 v0.7.2，仅使用 **ASS → libass / ass2bdnxml → BDN / PNG → SUPer → SUP**。支持 macOS 15 及以上的 Apple Silicon（arm64）Mac；不再提供 Intel（x64）版本。最低系统版本按原生依赖构建环境确定。

界面参考 Windows 版的任务表格与分区边框：蓝灰网格背景、深蓝标题栏、带表头和网格线的任务队列、独立转换设置和日志面板。任务队列位于左侧，设置位于右侧；较小窗口中设置可以滚动，开始 / 取消、进度和日志始终可见。

## 使用

打开 DMG，将 `SupCraft.app` 拖入“应用程序”，或直接解压 ZIP。程序包内包含 .NET 运行时、ASS 渲染器、Python 编码器与原生依赖，使用时无需安装 Homebrew、Python 或 AviSynth。

首版使用本机临时签名，没有 Apple Developer ID 公证；首次运行可能需要在系统“隐私与安全性”中允许打开。

1. 点击“添加 ASS…”或将 ASS 拖入窗口，支持多选和批量转换。
2. 选择输出目录及可选字体目录，然后检查字体。
3. 设置分辨率、与视频一致的帧率、SUP / BDN / 同时输出。
4. 开始转换，查看任务状态与详细日志；双击任务查看结果或错误。

转换期间参数与队列锁定。取消或关闭窗口会停止当前任务及其子进程，已完成输出保留。字体检查可以导出报告，并收集能够定位到文件的可用字体。

## Mac 版的功能与限制

- 不包含 AviSynth、VSFilterMod 或兼容模式选择。
- **没有 GPU 开关。**每个 SUP 任务固定配置 `use_gpu=1`，默认请求 OpenCL 加速图像相似度计算。设备不可用或 OpenCL 图像计算失败时，记录原因并回退 CPU；不会将 libass 渲染改为 GPU，也不是 Metal 编码。
- 字体目录递归读取 TTF / OTF / TTC，将字体交给 libass，不安装到系统。使用 CoreText 和系统字体目录检查系统字体；部分系统字体族无法收集为独立文件。
- 分辨率为 720p、1080p，以及仅 BDN 的 1440p、2160p；后两项仍由 1080p 图像与坐标缩放，非原生高分辨率渲染。
- 帧率：23.976、24、25、29.97、50、59.94 fps。
- HDR / BT.2020 和亮度缩减延续 Windows 现代模式的行为，不提供完整 PQ / HLG 色彩管理。
- “调色板 PNG”仅用于 720p / 1080p 的仅 BDN 输出；SUP 与高分辨率 BDN 使用 RGBA 中间图像。兼容模式的裁剪、PG 缓冲优化和严格写出选项不出现在 Mac 版。
- 跳过字段结构正确、Text 为空的 Dialogue；非空时间错误和字段缺失仍报错。临时转换副本统一为 UTF-8，原 ASS 不修改。
- 核对字幕尾部，必要时仅从临时副本尾部移除 `\fad` 后重试。检查 SUP 段结构、尾部时间和 BDN 图像引用。
- 已有同名 SUP / XML 自动增加数字后缀，PNG 使用任务前缀，避免覆盖已有输出。

日志保存到 `~/Library/Logs/SupCraft/`；GPU / Numba 缓存位于 `~/Library/Caches/SupCraft/`。失败或取消的任务目录保留在系统临时目录下的 `SupCraft/`，路径写入日志。任务独立配置存放在该目录，不修改应用包内的文件。

## 从源码构建

需要 Apple Silicon Mac、macOS 15+、原生 arm64 的 .NET 10 SDK、Python 3.11、Xcode Command Line Tools 和 Homebrew。请在原生 arm64 终端运行，勿使用 Rosetta。在源码根目录执行：

```sh
brew install libass libpng libimagequant pkg-config meson ninja
dotnet run --project macos/SupCraft.Checks -c Release
python3.11 macos/build_macos.py
```

构建脚本固定上游源码提交与 Python 主要依赖，生成原生渲染器、冻结编码器、自包含 `.app`、ZIP、DMG 和 SHA256 清单。默认输出为 `macos/dist/`。构建会启动原生界面，并实际验证目录字体渲染、索引 PNG、高分辨率 BDN 和 SUP + BDN 转换；失败时停止打包。

GitHub Actions 工作流仅在 Apple Silicon Mac 上执行构建和转换检查，通过后提供 arm64 DMG / ZIP 安装包。构建脚本会拒绝非 arm64 环境；工作流产物保留 30 天。

## 组件与源码

界面使用 Avalonia；图像缩放使用 SkiaSharp。转换组件为 [ass2bdnxml](https://github.com/cubicibo/ass2bdnxml) 和 [SUPer v0.2.8](https://github.com/cubicibo/SUPer/tree/v0.2.8)。准确源码版本见 `upstream-pins.json`。

SUPer 的适配只增加按任务读取配置的入口及 OpenCL 错误回退；回退也适用于独立编码进程，不改变编码参数算法。应用资源中包含适配源码、上游许可和版本记录；完整适配代码位于 `macos/runtime/`。原应用名称和图标保留。

## 授权与鸣谢材料

SupCraft 自有代码采用 0BSD，可免费或商业使用、修改和分发，无需额外申请。第三方组件仍使用各自许可证，感谢 libass、ass2bdnxml、SUPer、Avalonia、SkiaSharp 及所有依赖的作者和维护者。

下载包只包含 `SupCraft.app`（DMG 另有“应用程序”安装入口）。README、鸣谢、授权说明、审计与源码归档在 GitHub 查阅，不另附说明文件或 legal ZIP。第三方组件必须保留的许可文本及实际依赖记录保存在应用内部 `Contents/Resources/legal/`，源码位置见其中 `SOURCE-LOCATION.txt`。

准确来源、适用许可和尚需核对的传递依赖见项目的 `legal/SOURCES.md`。已有材料不等于已完成所有历史二进制的对应源码确认；原声明和 GPL / LGPL / MPL 条件保持有效。
