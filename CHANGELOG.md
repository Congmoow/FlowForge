# Changelog

所有重要变更都会记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，提交信息遵循 Conventional Commits。

## [Unreleased]

### Added

- 初始化 FlowForge 仓库骨架、解决方案结构与测试项目。
- 完成 Stage 1 骨架：Avalonia 应用壳、DI 与 ReactiveUI 集成、三栏主窗口、静态自绘画布节点、CI、Roslyn 分析和 ADR-0001/0002。
- 完成 Stage 2 节点与连线 UI：节点/端口/连线 ViewModel、节点模板、Toolbox 投放、节点拖动、多选、端口拖拽连线、类型兼容校验和贝塞尔连线渲染。
- 扩展 App 层无头单元测试，覆盖 CanvasViewModel、NodeViewModel、PortViewModel、EdgeViewModel、ToolboxViewModel 与贝塞尔几何。
- 完成 Stage 3 执行引擎：受控 Workflow 聚合、稳定 Kahn 拓扑排序、每边独立 Channel 路由、流式并发调度、异常传播与 200ms 协作取消。
- 新增 CSV/文本数据源、JSON 解析、文本拼接和控制台输出节点，控制台节点支持完整输入流与结构化 JSON 输出。
- 添加 Run/Stop 命令、节点 idle/running/success/failed 四态高亮，以及预置 CSV→Console 桌面执行入口。
- 新增 `samples/csv-to-console.ffw`、真实 CSV 数据和端到端集成测试，并接受 ADR-0003 Channel 执行契约。
- 完成 Stage 4：跨平台密钥存储、OpenAI/DeepSeek 节点、`.ffw` schema v1 与迁移、文件菜单、撤销重做和 CSV 摘要集成样例。
