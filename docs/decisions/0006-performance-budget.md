# ADR-0006: 性能预算与证据门禁

**Status**: Accepted  
**Date**: 2026-08-05

## Context

FlowForge 是面向作品集展示的桌面应用。性能目标不能只依靠主观感受，也不能用无头单元测试伪造 GUI 帧率。Stage 5 已经引入了 world/view 视口、节点和连线剔除、retained operation、确定性压力场景和 FPS HUD；Stage 6 需要把这些指标固化为可复查的预算和发布门禁。

## Decision

采用以下 v0.1 性能预算：

| 指标 | 预算 | 测量口径 |
| --- | ---: | --- |
| 冷启动到主窗口可交互 | < 1.5 s | Release 应用在 `App.OnFrameworkInitializationCompleted` 的 Stopwatch。 |
| 1000 节点画布平移 | ≥ 55 FPS | 真实 Release Avalonia 窗口，固定窗口尺寸、确定性场景、实际 `NodeCanvas.Render` 计数。 |
| 100 节点工作流加载 | < 200 ms | 从文件读取、迁移、NodeRegistry 创建和边校验的完整路径。 |
| 1000 节点空闲内存 | < 300 MB | 真实进程 `Process.WorkingSet64` 峰值；不使用托管堆估算替代。 |
| LLM 节点首次响应 | < 500 ms（不含网络） | 节点内部 Stopwatch；网络等待单独记录，不把网络延迟混入本地预算。 |

预算的测量与代码版本、运行时、RID、窗口尺寸、节点数、seed 和采样参数一起保存。100/250/500/1000 节点性能场景使用相同窗口、seed `20260803`、每档 5 秒预热、随后 30 秒自动平移、每秒一个样本；原始 JSON 是性能结论的主证据，HTML/PNG 是可读摘要。

## 证据规则

1. 真实 Release 窗口采样优先于无头模拟；无头测试只验证剔除、bounds、operation 生命周期和统计逻辑。
2. `SceneInvalidated` 的 root 级通知、`PushClip`、待处理矩形或 `LastDirtyRect` 不能单独证明 Avalonia renderer 只重绘局部区域。
3. 性能报告必须保留原始样本、环境信息和被测 commit；缺少其中任意一项时只能标记为未验证。
4. 本机一次成功运行不代表所有平台 GUI 启动已验证；跨平台构建和跨平台 GUI 启动分别报告。
5. 任何 PR 使预算指标退化超过 10% 时，必须先优化或记录新的 ADR 决策，再进入发布流程。

## Consequences

- Positive: 画布性能和发布质量有明确、可重复和可审查的目标；性能结论可以追溯到真实窗口和原始 JSON。
- Positive: 明确区分代码行为测试、渲染器证据和平台发布证据，减少过度解读。
- Negative: 真实窗口采样受操作系统、GPU、窗口管理器和当前负载影响，需要保存环境信息并谨慎比较。
- Negative: 启动、加载和 LLM 本地延迟需要额外的测量入口，不能从 FPS 采样推导。
- Trade-offs: v0.1 采用固定场景和固定 seed 换取可比较性；后续若支持更多硬件或动态场景，应扩展报告格式而不是覆盖旧证据。

## Validation

当前仓库已有的真实 Release 性能报告记录了 1000 节点平均 FPS `68.216`、峰值 `WorkingSet64` `290,734,080` bytes；完整原始数据见 [`docs/perf/fps-vs-node-count.html`](../perf/fps-vs-node-count.html)。这些数据证明该次采样的 FPS 和内存预算通过，但不替代启动时间、工作流加载时间、LLM 首次响应和跨平台 GUI 启动的单独验证。
