# ADR-0005: 插件加载隔离与节点注册策略

**Status**: Proposed
**Date**: 2026-08-03

## Context

FlowForge 需要让外部 DLL 提供节点定义，同时保持 `FlowForge.Core` 公共契约的类型
身份稳定、避免插件依赖污染主进程，并允许坏插件不阻断应用启动。当前节点注册表
只接受单个工厂，不能统一服务 Toolbox、序列化、运行和属性面板，也没有插件加载
上下文、诊断或卸载边界。

## Decision

- 插件必须显式实现 `INodePlugin`，返回 `IReadOnlyList<NodeDefinition>`；不扫描任意
  无参 `INode` 类型。
- `PluginLoader` 为每个插件使用带 `AssemblyDependencyResolver` 的可卸载自定义
  `AssemblyLoadContext`。解析 `FlowForge.Core` 时始终回退到 Default context，保证
  `INode`、`IPort`、`NodeDefinition` 和 `INodePlugin` 类型身份一致。
- 按顶层 `plugins` 目录中的 DLL 文件名稳定排序扫描。单 DLL、
  `ReflectionTypeLoadException`、缺依赖和重复 TypeId 只形成结构化诊断；单个插件的
  definition 先完整校验，成功后以原子批次注册，失败不留下部分注册。
- v0.1 不做热更新；加载上下文保持到应用关闭，由应用统一释放并允许卸载验证。
- `NodeRegistry` 暴露 definition 枚举、`CreateDefault` 和原子 `RegisterRange`，并
  保留既有 `Register/Create` 兼容 API；公共 `PortFactory` 供内置节点和插件创建
  `IPort<T>`。

## Consequences

- Positive: 插件可以复用 Core 类型而不复制 Core 程序集；坏插件可诊断、跳过，其他
  插件和应用仍可启动；所有节点消费者共享同一目录和 TypeId 规则。
- Positive: 可卸载上下文减少长期运行时的程序集泄漏风险，并为应用关闭时的清理提供
  明确边界。
- Negative: AssemblyLoadContext 的解析、诊断聚合和卸载测试比简单反射扫描复杂；
  插件不能假设任意依赖会从主应用目录自动获得。
- Trade-offs: v0.1 维持上下文到应用关闭，暂不支持热更新，以换取注册和执行期间的
  类型身份稳定性。

## Validation Plan

实现完成后补充有效、损坏、缺依赖、重复 TypeId、Core 类型身份、原子注册、示例插件
加载和卸载测试；在这些测试及应用关闭 smoke 通过前，本 ADR 不得改为 `Accepted`。
