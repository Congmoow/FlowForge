# ADR-0002: 选用 ReactiveUI 作为 MVVM 基础

**Status**: Accepted
**Date**: 2026-06-10

## Context

FlowForge 的 UI 会包含节点选择、属性编辑、画布状态、命令执行状态和运行反馈。ViewModel 需要清晰表达状态变化，并支持后续扩展到撤销/重做、节点高亮、运行状态订阅等交互。项目规范已固定 MVVM 选型为 ReactiveUI 19.x。

## Decision

使用 ReactiveUI 19.x 作为 ViewModel 基础。ViewModel 继承 `ReactiveObject`，命令和可观察状态在后续阶段按功能逐步引入。依赖注入使用 `Microsoft.Extensions.DependencyInjection`，不引入额外 IoC 容器。

## Consequences

- Positive: ReactiveUI 适合表达异步状态、命令和响应式 UI 变化。
- Positive: 与 Avalonia 的 MVVM 模式契合，便于后续测试 ViewModel 而不依赖 UI 线程。
- Negative: ReactiveUI 的响应式模型比简单属性绑定更有学习成本。
- Trade-offs: 当前保持最小集成，优先直接引用 ReactiveUI 19.x；若未来需要 Avalonia.ReactiveUI 集成包，必须先处理其与 ReactiveUI 19.x 的版本兼容问题。
