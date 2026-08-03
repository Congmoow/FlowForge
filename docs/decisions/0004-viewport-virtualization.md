# ADR-0004: 视口导航、虚拟化与增量绘制策略

**Status**: Proposed
**Date**: 2026-08-03

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
  区域；使用 `IRenderer.SceneInvalidated.DirtyRect` 与
  `RendererDebugOverlays.DirtyRects` 进行 renderer 级证据验证。
- 视口算法和 operation 使用无头单元测试；真实 dirty rect 与 1000 节点帧率只在
  Avalonia renderer/Release dev-perf 场景中测量，不在单元测试中伪造 FPS。

## Consequences

- Positive: 坐标转换、命中、剔除和绘制边界共享一套可测试规则；无关节点和边不进入
  当前帧场景，节点局部变化不必重建整幅 retained scene。
- Positive: dirty rect 具有渲染器级观测证据，便于发现整画布失效回退。
- Negative: Avalonia 11.2.3 没有可直接使用的 `InvalidateVisual(Rect)` API，需要在
  retained operation 与 renderer invalidation 边界上实现和验证局部更新。
- Trade-offs: operation 快照和 Geometry 缓存会增加生命周期管理复杂度，但换取稳定
  的 bounds、比较和资源释放语义。

## Validation Plan

实现完成后补充真实测试路径、操作文件和性能数据；在没有 renderer 级 dirty rect
与 Release 1000 节点测量前，本 ADR 不得改为 `Accepted`。
