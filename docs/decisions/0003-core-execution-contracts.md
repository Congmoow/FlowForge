# ADR-0003: 核心执行与 Channel 路由契约

**Status**: Accepted
**Date**: 2026-07-10

## Context

Stage 3 需要先定义节点、端口、执行上下文和工作流领域模型，后续的边路由器、调度器、序列化与撤销命令都会依赖这些公共契约。现有规范已经确定 `INode` 的主体形状、强类型端口和 `.ffw` 边字段，但尚未完整明确执行上下文读写、Channel 生命周期、并发调度和失败传播语义。

Stage 3 验收文字要求 CSV→TextConcat→Console，但节点协议规定 CSV 输出为行字典集合，TextConcat 输入为 `string[]`，且端口禁止自动转换。直接实现该链路会破坏已经固化的类型边界。

## Decision

- 使用 `INodeConfig` 作为所有可序列化节点配置的标记接口。
- `IPort` 暴露稳定字符串 ID、显示名称和运行时数据类型，`IPort<T>` 继承 `IPort` 并表达强类型端口。
- `IExecutionContext` 通过 `ReadAsync<T>`、`ReadAllAsync<T>` 和 `WriteAsync<T>` 接收强类型端口与 `CancellationToken`，分别提供单值读取、流式读取和单项写入；通道完成、异常传播和生命周期由后续边路由器与调度器管理。
- `Workflow` 是受控可变聚合，内部维护节点和边集合，仅通过 `AddNode`、`RemoveNode`、`AddEdge` 和 `RemoveEdge` 修改，外部只能读取集合视图。
- `WorkflowEdge` 使用工作流文件格式中的边 ID、源节点 ID、源端口 ID、目标节点 ID 和目标端口 ID。
- 连接兼容性采用 `targetType.IsAssignableFrom(sourceType)`，允许精确类型和可赋值的派生类型，不执行自动类型转换。
- `Workflow` 通过领域异常报告节点或边 ID 重复、节点或端口引用无效、端口方向错误、端口类型不兼容和输入端口重复连接；自环及更复杂的环由后续 Kahn 拓扑排序统一检测。
- 每条 `WorkflowEdge` 使用独立的无界 `Channel<object?>`，设置 `SingleWriter = true` 和 `SingleReader = true`；输出端口 fan-out 时由路由器向每条边广播。
- 选择 `System.Threading.Channels` 而不是 Rx Subject。Channel 原生提供异步背压接口、完成与异常传播、取消令牌支持，不需要在 Core 层引入额外响应式依赖。
- 节点成功时完成其全部输出 Channel；节点失败时使用同一异常完成输出 Channel，并由调度器取消其余节点。
- 调度器在确认拓扑无环后并发启动全部节点。下游节点通过 Channel 等待输入，因此输出一旦写入即可流式消费，不等待上游节点完全结束。
- Stage 3 的 UI 运行按钮执行预置 CSV→Console 工作流，不提前实现 Stage 5 的属性面板或任意画布到 Core 的配置映射。
- Stage 3 集成测试加载并校验真实 `samples/csv-to-console.ffw`，再使用等价 Core 工作流执行；完整 `.ffw` 反序列化仍属于 Stage 4。
- CSV→Console 作为 Stage 3 端到端验收，TextConcat 的 `string[]` 强类型输入由独立节点测试覆盖，从而保留节点协议的类型约束。

## Consequences

- Positive: 后续执行引擎可围绕稳定、强类型的读写契约实现流式 Channel 路由。
- Positive: 非法引用和端口连接会在进入调度器前被工作流聚合拒绝。
- Positive: 每条边独立完成和传播异常，fan-out 不会让多个下游竞争同一个 ChannelReader。
- Negative: 工作流修改必须通过领域方法，UI 与序列化层不能直接替换内部集合。
- Negative: 无界 Channel 不提供容量背压，Stage 3 依靠取消和节点消费速度控制内存；有界容量策略留给性能阶段评估。
- Trade-offs: 预置 UI 示例验证执行链路和状态反馈，但任意画布配置执行要等属性面板和持久化边界完成后再接入。
- Trade-offs: Stage 3 基础模型允许暂存有环图，以便编辑器保存中间状态；执行前必须显式运行拓扑排序并报告环错误。
