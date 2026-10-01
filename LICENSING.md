# 使用与授权

SupCraft 的自有代码采用 [0BSD（Zero-Clause BSD）](LICENSE)：任何人均可免费或商业使用、复制、修改及分发，无需向作者另外申请授权，自有代码不要求署名或保留版权声明。软件按现状提供，不作担保。

这一授权覆盖项目自有的界面、转换协调代码、构建脚本、测试、文档、应用图标，以及 `macos/runtime/` 中新增的独立适配文件。它不改变第三方组件、第三方源码或在上游 GPL 文件基础上形成的修改版的授权。

`third_party/` 中的代码与二进制、`legal/licenses/`、上游源码归档、第三方 NuGet / Python 包和原生库，继续使用各自许可证。SUPer 的修改版 `supercli.py` 与 `SUPer/utils.py` 保留原 GPLv3-or-later 文件头；新增适配代码的 0BSD 与其 GPL 组合兼容，后端组合仍按 GPL 要求处理。

第三方鸣谢见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，许可文本见 [legal/licenses/](legal/licenses/)，源码、版本与修改记录见 [legal/SOURCES.md](legal/SOURCES.md) 和 [legal/MODIFICATIONS.md](legal/MODIFICATIONS.md)。

允许自由使用 SupCraft，不代表可以删除第三方声明或把所有依赖重新标记为 0BSD。分发第三方 GPL / LGPL / MPL 组件时，仍需遵守其源码提供和其他条件。用户字幕、商业字体和输出素材不由本项目代为授权。

授权材料提供源码的方式、尚未确认的二进制来源及传递依赖均明确记录；不能仅凭鸣谢或仓库公开认定所有第三方发行义务已经满足。
