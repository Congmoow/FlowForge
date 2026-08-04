# ADR-0004: 视口导航、虚拟化与增量绘制策略

**Status**: Accepted
**Date**: 2026-08-04

## Context

Stage 5 需要让画布在节点数量达到 1000 时仍可交互。当前 `NodeCanvas` 使用固定
屏幕坐标绘制网格、节点和连线，节点移动会使整个控件重新绘制，且没有统一的
world/view 坐标模型。仅在绘制阶段 PushClip 或保存待处理矩形，不能证明渲染器只
处理了局部场景，也不能解决负坐标、缩放、平移和边界命中之间的不一致。

## Decision

- 使用 `CanvasViewport` 与 `ViewportTransform` 统一 world/view 坐标、逆变换命中、
  0.25x-4x 缩放、鼠标中心缩放、空格左拖/中键平移和自适应网格。
- 节点和贝塞尔边使用纯函数 world bounds 做视口相交剔除。节点相交包含边界；边
  bounds 包含端点与两个控制点，并按 stroke 宽度膨胀。
- 使用按节点和按边拆分的 `ICustomDrawOperation` retained scene。operation 持有
  不可变快照、紧 bounds 和可缓存的 `FormattedText`/`Pen`/`Geometry`，并实现稳定
  `Equals` 与正确 `Dispose`。
- 节点移动只替换移动节点和关联边的 operation，以旧/新 bounds union 作为失效
  区域。每个可见节点和连线由独立的 retained 子 visual 承载，并启用
  `CompositionOptions.UseRegionDirtyRectClipping`。
- Avalonia 11.2.3 没有 `InvalidateVisual(Rect)` API；`IRenderer.SceneInvalidated`
  是 render-root 级通知，不能被解释成局部区域 API。使用
  `RendererDebugOverlays.DirtyRects` 打开 renderer 诊断，并读取同一 renderer
  的实际 dirty tracker 记录验证底层局部更新，不使用 PushClip、pending rect 或
  `RetainedCanvasScene.LastDirtyRect` 冒充 renderer 证据。
- 视口算法和 operation 使用无头单元测试；真实 dirty rect 与 1000 节点帧率只在
  Avalonia renderer/Release dev-perf 场景中测量，不在单元测试中伪造 FPS。

## Consequences

- Positive: 坐标转换、命中、剔除和绘制边界共享一套可测试规则；无关节点和边不进入
  当前帧场景，节点局部变化不必重建整幅 retained scene。
- Positive: retained 子 visual 的 renderer dirty tracker 具有局部观测证据，便于
  发现整画布失效回退；公开 `SceneInvalidated` 的 root 级边界也被明确记录。
- Negative: Avalonia 11.2.3 没有可直接使用的 `InvalidateVisual(Rect)` API，需要在
  retained operation 与 renderer invalidation 边界上实现和验证局部更新。
- Trade-offs: operation 快照和 Geometry 缓存会增加生命周期管理复杂度，但换取稳定
  的 bounds、比较和资源释放语义。

## Validation Plan

已完成的可复现验证包括 `tests/FlowForge.App.Tests/Canvas/CanvasCullingTests.cs`、
`BezierBoundsTests.cs` 和 `DirtyRegionTests.cs`，以及
`docs/perf/fps-vs-node-count.html` 中记录的 Release 窗口测量。

2026-08-04 的真实 renderer smoke 使用 1200 × 760 窗口、DPI 2 和独立节点
retained 子 visual：`IRenderer.SceneInvalidated.DirtyRect` 报告 1200 × 760，
这是 Avalonia 11.2.3 的 render-root 通知边界；同一 renderer 的内部 dirty
tracker 在节点移动时记录了旧区域 200,240,440 × 192 与新区域 280,300,440 × 192，
均小于整画布。`RendererDebugOverlays.DirtyRects` 已启用用于该 renderer 诊断。
该证据证明实际子 visual 失效没有退化为待处理矩形或 PushClip 推测，同时明确
公开事件不能单独证明局部区域，因此保留该框架边界作为本决策的限制。
