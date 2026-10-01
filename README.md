# SupCraft

**ASS → SUP / BDN 图形字幕批量转换器**

本程序由codex编写

SupCraft 是 Windows 桌面字幕转换工具，可将 ASS 字幕中的文字、字体、排版和渲染器支持的特效转换为图形字幕。程序提供 AviSynth / VSFilterMod 兼容模式，以及 libass / SUPer 现代模式，支持批量任务、免安装字体目录、字体检查和输出验证。

Mac 版仅提供 Apple Silicon（arm64）应用包，仅使用 libass / SUPer 路线，无需 AviSynth。GPU 加速默认请求开启，界面不提供开关；OpenCL 不可用时自动回退 CPU，并记录原因。Mac 版保留批量转换、字体目录、字体检查、HDR、输出验证和尾部保护，沿用原应用图标。v0.7.1 增加网格背景、任务表格网格线、分区边框和独立日志面板；不再构建 Intel / x64 版本。

| 输出格式 | 内容与用途 |
| --- | --- |
| SUP | PGS 图形字幕流，用于支持该格式的播放、封装或制作工具 |
| BDN XML + PNG | 字幕时间、位置等信息写入 XML，字幕图像保存为 PNG，供支持 BDN 的制作工具导入 |
| SUP + BDN XML / PNG | 同一任务同时生成上述两种输出 |

## 快速开始

1. 解压运行包，启动根目录的 `SupCraft.exe`。保留 EXE 与旁边 `tools/` 文件夹的相对位置。
2. 点击“添加 ASS…”一次选择多个文件，或把 ASS 拖入窗口。
3. 选择输出目录。首次添加文件时，程序默认使用第一个 ASS 所在目录下的 `subtitle-output`。
4. 如有配套字体，选择“字体目录（可选）”；程序会读取其中的子文件夹。留空则使用系统字体。
5. 点击“检查字体…”查看缺失项，按字幕的制作环境选择转换核心。
6. 设置分辨率、与视频一致的帧率和输出格式，再点击“开始批量转换”。
7. 查看任务列表中的完成或失败状态；双击任务可查看输出路径或错误摘要。

任务按顺序执行，单个文件失败后会继续处理下一项。转换期间队列和参数会锁定；“取消当前批次”停止当前任务与后续任务，已完成输出保留。关闭转换中的窗口会先取消批次并清理临时字体和配置。

## 运行环境

- 使用 Windows，现代 SUP 编码所附带的 SUPer 需要 **64 位 Windows**。
- **兼容模式需要另外安装 32 位 AviSynth+**；只安装 64 位 AviSynth+ 无法供本程序的兼容核心使用。
- **现代模式不需要安装 AviSynth**。运行包已附带 ass2bdnxml、SUPer 及其运行依赖，无需另外安装 Python。
- 应用目录、输出目录和临时目录需要可写；现代 SUP 编码会临时修改并恢复 `tools/super/config.ini`。
- 无独立显卡也可以使用默认的 CPU 模式。GPU 加速另需兼容的 OpenCL 设备、驱动及运行时。

## 两种转换模式

| 项目 | 兼容模式 | 现代模式 |
| --- | --- | --- |
| 界面中的核心名称 | AviSynth / avs2bdnxml 2.08 | libass / ass2bdnxml / SUPer |
| 转换流程 | ASS → AviSynth / VSFilterMod → avs2bdnxml → SUP 或 BDN | ASS → libass / ass2bdnxml → BDN / PNG；输出 SUP 时再交给 SUPer 编码 |
| 适合的字幕 | 按 VSFilter / VSFilterMod 效果制作和校对的字幕 | 按 libass 效果制作和校对的字幕 |
| 主要特点 | VSFilterMod 渲染；核心可直接输出 SUP；提供裁剪和 PG 缓冲选项 | 免 AviSynth；渲染与 SUP 编码分离；提供 HDR / BT.2020 与可选 GPU 相似度计算 |
| 依赖与成本 | 需要 32 位 AviSynth+；复杂字幕受旧核心能力限制 | 先生成 PNG，复杂动画可能占用较多临时空间；SUP 编码还有分析开销 |
| 可选分辨率 | 720p、1080p | 720p、1080p；1440p、2160p 仅输出 BDN |
| 字体目录 | 在当前 Windows 会话临时加载，批次结束后卸载 | 通过渲染器字体目录直接读取，不注册到系统会话 |

两种渲染器在换行、字距、描边、字体替代及扩展标签上可能有差异，不能保证逐像素一致。按 VSFilterMod 制作的特殊效果，应先用兼容模式检查；使用现代模式时，应抽查这些效果。转换速度取决于字幕复杂度和电脑配置，现代模式不保证更快。

PGS 的调色板、缓冲及解码时序限制，也可能影响高频动画的保留效果。文件结构验证通过不代表所有画面、颜色和特效都与原 ASS 一致。

更详细的对比与技术依据见 [转换模式、输出选项与字体目录](MODES-AND-OPTIONS.md)。

## 转换参数

| 参数 | 可选值或作用 |
| --- | --- |
| 分辨率 | 720p（1280×720）、1080p（1920×1080）；现代模式另有 1440p（2560×1440）、2160p（3840×2160）BDN |
| 帧率 | 23.976、24、25、29.97、50、59.94 fps；需要自行按目标视频设置 |
| 输出格式 | SUP、BDN XML + PNG、SUP + BDN XML / PNG |
| 语言代码 | 写入输出元数据，默认 `zho`；不会翻译字幕 |
| HDR / BT.2020 | 仅现代模式：渲染时缩减字幕 RGB 亮度；输出 SUP 时使用 BT.2020 矩阵 |
| 亮度缩减 % | HDR 选项开启后可设置 0–100%，默认 33% |
| GPU 加速（OpenCL） | 默认关闭；仅现代模式包含 SUP 输出时可选，控制 SUPer 的图像相似度计算 |

默认参数为兼容模式、1080p、23.976 fps、SUP。程序不会读取视频来自动识别分辨率或帧率。

**1440p / 2160p BDN 是将 1080p 渲染图像、坐标和画布同步缩放得到的结果，并非原生高分辨率 libass 渲染。**选择这两项后，界面会锁定为 BDN 输出，不能生成对应分辨率的 SUP。

HDR 选项提供字幕亮度缩减和 SUP 编码矩阵选择，未实现完整的 PQ / HLG 色彩管理。

## 字体目录与字体检查

选择字体目录后，程序递归读取 **TTF、OTF、TTC** 文件，同时识别字体内部名称和别名。目录可包含中文、空格和子文件夹，无需将字体永久安装到 Windows。

- **兼容模式：**批次开始时临时加载字体，完成、失败或取消后卸载。加载期间，其他程序也可能看到这些会话字体。
- **现代模式：**将字体复制到任务临时目录，再通过 `--fontdir` 交给渲染器，不向 Windows 注册字体。
- **检查字体：**汇总 ASS 样式和 `\fn` 行内引用，列出系统或目录中的可用字体、缺失字体及逐文件结果。
- **导出报告：**保存 UTF-8 文本报告。
- **收集可用字体：**将能够定位到文件的已引用字体复制到所选目录；不能保证每个字体族都能收集到文件。

缺失字体会记录警告，转换仍可能使用替代字体继续，字宽、换行和位置也可能改变。ASS 必须引用正确的字体名称；选择目录不能修复损坏的字体、缺字或错误引用。同名系统字体与目录字体并存时，优先级由渲染器决定，程序不保证强制覆盖。

## 字幕预检与尾部保护

程序读取 UTF-8（有或无 BOM）字幕；UTF-8 解码失败时尝试 GBK。每个任务会检查 Events 字段结构、非空事件的时间格式及时间范围，并识别脚本分辨率与字体引用。

空事件处理（自 v0.6.1 起）：

- 跳过字段结构正确、`Text` 为空或全为空白的 `Dialogue`。
- 被跳过的事件不计入有效事件数，也不用于计算最后结束时间；日志汇总数量和原始行号。
- 仅清理转换时的临时副本，**原 ASS 保持不变**。
- 包含标签或绘图命令的事件仍保留检查；非空事件时间错误、字段缺失仍会报错。
- 全部事件均为空时，任务会提示没有可转换内容。

兼容模式按最后结束时间额外增加 2 秒渲染长度。现代模式会核对 BDN 尾部；检测到末尾缺失时，尝试仅移除临时副本中尾部事件的 `\fad` 后重试。仍无法满足尾部检查时报告失败。

### 常见问题

**提示 AviSynth 无法加载**

检查是否安装了 32 位 AviSynth+。若使用现代模式，则不需要 AviSynth；组件缺失时应重新解压完整运行包。

## 源码构建与检查

源码目录包含 `src/`、`assets/`、`third_party/`、`tests/` 及构建脚本。构建使用 Windows .NET Framework 的 C# 编译器，生成 x86 WinForms 程序：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

输出为 `app/SupCraft.exe`，依赖组件会从 `third_party/` 复制到 `app/tools/`。

## 使用的组件

| 组件 | 本版本用途 |
| --- | --- |
| [avs2bdnxml 2.08](https://github.com/koroban/avs2bdnxml) | 兼容模式 SUP / BDN 写出；运行包附带 GPL-3.0 许可文本 |
| VSFilterMod | 兼容模式 ASS 渲染，随包提供 `S-VSFilterMod.dll` |
| [ass2bdnxml v0.7h](https://github.com/cubicibo/ass2bdnxml) / libass | 现代模式 ASS 渲染与 BDN 生成 |
| [SUPer v0.2.8](https://github.com/cubicibo/SUPer/tree/v0.2.8) | 现代模式 PGS 编码，保留随包的 PyOpenCL 可选组件 |

`third_party/SupMover` 保存了 [SupMover](https://github.com/MonoS/SupMover) 的参考源码，目前未接入 GUI，也未分发其可执行文件。各第三方组件的授权信息以其附带许可文件为准。

感谢上述项目及 Avalonia、SkiaSharp、Python、.NET、Pillow、NumPy、OpenCV、PyOpenCL 等项目的作者和维护者。README、完整鸣谢、授权说明及修改记录集中保存在 GitHub；原始源码材料保存在仓库及独立源码包中。必须随组件保留的第三方许可文本位于 Windows 的 `tools/licenses/` 和 Mac 应用内部的 `Contents/Resources/legal/licenses/`。已确认和仍需核对的源码来源见 [legal/SOURCES.md](legal/SOURCES.md)，不以鸣谢代替 GPL / LGPL / MPL 的源码义务。

SupCraft 自有代码采用 **0BSD**：可免费或商业使用、复制、修改和分发，无需额外申请授权。第三方组件保留各自许可；详见[使用与授权](LICENSING.md)、[第三方鸣谢](THIRD-PARTY-NOTICES.md)和[源码及发行材料](legal/SOURCES.md)。
