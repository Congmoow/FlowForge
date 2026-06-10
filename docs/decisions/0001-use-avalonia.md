# ADR-0001: 选用 Avalonia 构建桌面端

**Status**: Accepted
**Date**: 2026-06-10

## Context

FlowForge 的目标是本地运行的节点式 AI 工作流编辑器，需要跨 Windows、macOS、Linux 的桌面体验，并且需要高频画布绘制、键鼠交互和后续打包发布能力。项目明确不做 Web 版、移动端和云端托管，因此技术栈应优先服务原生桌面交互与作品集展示。

## Decision

使用 Avalonia 11.2.x 构建桌面端 UI。应用层放在 `FlowForge.App`，核心领域与执行逻辑保持在无 UI 依赖的 `FlowForge.Core` 中。画布相关控件优先使用 Avalonia 自绘能力实现，避免引入 WebView 或其他 Web 技术栈。

## Consequences

- Positive: 可在三大桌面平台复用 UI 代码，适合实现自绘画布和复杂键鼠交互。
- Positive: 与 .NET 8、MVVM、CI 和后续发布流程集成成本较低。
- Negative: 需要处理 Avalonia 特有的 XAML、样式和渲染细节，调试方式与 WPF 不完全相同。
- Trade-offs: 放弃 WPF 成熟生态，换取跨平台桌面能力和更清晰的作品集技术边界。
