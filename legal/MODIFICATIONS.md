# 第三方适配与修改记录

维护者：Enfana5780。初版适配：2026-10-01。适用上游版本见 upstream-pins.json。

## SUPer v0.2.8，GPLv3-or-later

- `supercli.py`：优先从 `SUPCRAFT_JOB_CONFIG` 环境变量读取每个任务独立的配置，缺省路径保持上游行为，避免写入只读的应用包。
- `SUPer/utils.py`：把 `SSIM_PIL.compare_ssim` 的导入改为 `gpu_fallback.compare_ssim`，使 macOS spawn 工作进程同样具有 OpenCL 错误回退。
- 新增 `macos/runtime/gpu_fallback.py`、`super_entry.py`：默认请求 GPU，设备不存在或 OpenCL 计算失败时记录原因并使用 CPU；与 OpenCL 无关的异常仍报告，不更改编码参数算法。这些独立适配文件继续保留 SupCraft 自有 0BSD 授权（见 [许可原文](SUPCRAFT-0BSD.txt)），不适用根目录新增的禁止转售条款；与 GPL SUPer 组成的编码器按 GPL 条件分发。
- 保留上游原始 GPL 文件头及版权声明。原始完整源码归档、生成后的适配文件和可重现替换的构建脚本随授权材料提供。

## 未改动源码的组件及包装变化

- ass2bdnxml：从固定提交编译，没有修改其 C 源码；传入字体目录和输出参数。
- Windows ass2bdnxml 发行物中的 `libunibreak-6.dll` 保留原文件，并复制为 `libunibreak-7.dll` 兼容发行物的导入名称；不宣称是新编译的版本。
- Mac 原生库：通过 install_name_tool 重定位依赖路径到应用资源内，使用本机临时签名；库算法源码未修改。脚本和版本记录用于描述这些包装变化。
- avs2bdnxml、VSFilterMod 和 Windows SUPer 二进制：未重新修改其算法。二进制对应源码来源的确认状态见 SOURCES.md。
- SupMover：仅保留原始参考源码与 AGPL 许可，未集成执行。

以上记录不改变原作者的许可。分发其他修改版本时，请同步记录修改作者、日期、内容和准确源码版本。
