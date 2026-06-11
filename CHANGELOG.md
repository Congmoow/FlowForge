# Changelog

所有重要变更都会记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，提交信息遵循 Conventional Commits。

## [Unreleased]

### Added

- 初始化 FlowForge 仓库骨架、解决方案结构与测试项目。
- 完成 Stage 1 骨架：Avalonia 应用壳、DI 与 ReactiveUI 集成、三栏主窗口、静态自绘画布节点、CI、Roslyn 分析和 ADR-0001/0002。
- 完成 Stage 2 节点与连线 UI：节点/端口/连线 ViewModel、节点模板、Toolbox 投放、节点拖动、多选、端口拖拽连线、类型兼容校验和贝塞尔连线渲染。
- 扩展 App 层无头单元测试，覆盖 CanvasViewModel、NodeViewModel、PortViewModel、EdgeViewModel、ToolboxViewModel 与贝塞尔几何。
