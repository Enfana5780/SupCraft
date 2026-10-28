# 构建与维护

[项目首页](../README.md) · [使用说明](USAGE.md)

以下路径和命令均相对于仓库根目录。

## Windows 构建与发布

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\package.ps1 -Suffix win
powershell -ExecutionPolicy Bypass -File .\tests\run-checks.ps1
python legal/check_materials.py
```

构建使用系统 .NET Framework 的 x86 C# 编译器，输出 `app/SupCraft.exe`，从 `third_party/` 复制转换组件到 `app/tools/`。AviSynth 路线需要 32 位 AviSynth+；SUPer 需要 64 位 Windows。

`VERSION` 使用三段版本号，当前为 `0.8.0`；Windows 文件 / 程序集版本为 `0.8.0.0`，窗口标题省略为 `v0.8`。补丁号非零时显示三段版本。

打包脚本在仓库父目录生成运行 ZIP、完整源码 ZIP 和 SHA256 清单。运行包不带日志、用户字幕或测试输出；源码包包含 `docs/`、`legal/` 和构建所需依赖，排除 Git 元数据、`qa/`、`bin/`、`obj/`、Mac vendor/dist。

## 代码分工

| 文件 | 职责 |
| --- | --- |
| `src/Program.cs` | ASS 预检、任务队列、字体目录、两条转换路线与输出验证 |
| `src/ConversionSupport.cs` | 临时字体、SUPer 配置锁及转换支持 |
| `src/WindowsUi.cs` | 分区布局、自绘控件、画布提示与批次计时 |
| `src/WindowChrome.cs` | 集成标题栏、原生拖动缩放及最大化 / 还原 |
| `src/UiScaling.cs`、`src/Appearance.cs` | DPI 缩放及浅色 / 深色主题 |
| `src/CleanNumeric.cs` | 数值编辑与禁用状态绘制 |
| `src/AssFonts.cs`、`src/FontManager.cs` | 字体引用解析、批量替换、备份及字体管理窗口 |
| `macos/` | 独立 Mac 界面、转换核心、运行时适配和打包 |

## 需保持的行为

- 默认 libass、1080p、23.976 fps、SUP；空输出路径表示逐个 ASS 的源目录。手动选择目录则统一输出；同一目标目录中的重名任务加后缀。
- 窗口以 1280×840、12 px 正文为基准，最小 1040×800。上方两栏高 552 px，设置宽 579 px，列表完整显示 8 行。横向新增空间给文件区，纵向新增空间给日志区。
- 当前核心决定可见选项。状态、已运行时长及进度条放在转换按钮上方；长状态可悬停查看。计时用 Stopwatch，每批重置，完成 / 失败 / 取消后停止，小时数不按 24 小时回绕。
- 控件从原始逻辑尺寸缩放，覆盖 96 / 144 / 192 DPI 和重复切换。主窗及字体对话框字号一致；切换主题保持参数与布局。
- AviSynth 自定义宽高各为 64–4096 且分别可被 4 整除，直接传给 MaskSubMod 原生渲染。编辑原始文字时立即校验；非标准画布有提示，错误时禁用开始。SUP / BDN 发布前核对画布和图形边界。
- libass 的 1440p / 2160p 仅 BDN，按 1080p PNG 和坐标缩放；不宣称原生高分辨率或蓝光合规。Windows 自定义尺寸不扩展到 Mac。
- 转换跳过结构正确且 Text 为空的事件，只修改临时副本；非空事件仍严格验证。libass 尾部检查失败时仅在副本移除尾部淡入淡出重试，失败保留诊断文件。
- 字体匹配读取 TTF / OTF / TTC 的本地化内部名称及集合中的所有字体面，精确匹配并保留标点。临时字体在完成、取消或失败后卸载；SUPer 配置加锁并恢复原始字节。
- 字体替换覆盖样式与行内字体标签，保留其他字段、编码、BOM 和换行。备份位于源文件同级 `backup/`，使用 `.ass` 后缀并避免覆盖旧备份；拒绝过期扫描和无法用原编码保存的名称。

## 检查

`tests/run-checks.ps1` 覆盖解析、两条路线实际转换、自定义画布、界面交互、主题、字体替换及同源输出。它包含 `ui-layout-check.ps1` 的不同宽度、字号和 DPI 检查。

可单独运行 `ui-layout-check.ps1 -FontSize 14` 检查大字号布局。可选 `python tests/verify-canvas-decode.py` 需要 FFmpeg 和 Pillow，验证自定义 SUP 解码、清屏及与 BDN 的像素对应。

## 文档与许可材料

根目录保留 README、LICENSE 和第三方声明。独立自有部分采用 `LicenseRef-SupCraft-Free-Use-No-Resale-1.0`，允许商业项目使用、禁止软件售卖或收费分发；`macos/runtime/` 保持 0BSD，第三方及 GPL 修改版不受新增限制，旧版已授予的权利保留。使用与开发文档位于 `docs/`；审计、来源、修改记录和许可正文位于 `legal/`。

`legal/license-index.json` 保留每个组件原始路径、实际正文路径、SHA256 和已有来源。仅逐字节相同的正文合并存储，版权内容不改写。`prepare_materials.py` 刷新前恢复别名，完成后重新去重；`check_materials.py` 校验所有映射及固定源码归档。

更新材料后同步运行包与源码包，检查相对链接及打包清单。历史来源待确认事项以 [SOURCES.md](../legal/SOURCES.md) 为准，修改第三方文件时更新 [MODIFICATIONS.md](../legal/MODIFICATIONS.md)。

## Mac

Mac 使用 .NET / Avalonia，仅构建 macOS 15+ Apple Silicon。GPU 默认关闭，可在 SUP 输出时显式开启；OpenCL 失败记录原因后回退 CPU；目录字体不注册到系统。实际包的 Python / NuGet 许可及原生构建记录由 `macos/collect_legal.py` 收集后放入应用内部，再签名；说明与上游归档集中在源码仓库，不另发 legal ZIP。原生构建命令见 [Mac 说明](../macos/README.md)。
