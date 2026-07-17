# ADR-0007: Stage 4 持久化与安全契约

**Status**: Accepted
**Date**: 2026-07-17

## Context

Stage 4 需要让工作流可安全保存、重新打开并继续编辑，同时新增需要 API Key 的 LLM 节点。现有 `Workflow` 只管理节点和边，画布位置、文件生命周期、运行时依赖和撤销历史尚未形成统一契约。

## Decision

- `.ffw` 使用 `apiKeySecretId` 保存不透明密钥引用，禁止保存或记录 API Key 明文。
- OpenAI 与 DeepSeek 均使用非流式 Chat Completions 协议；OpenAI 支持 `baseUrl`，DeepSeek 使用固定服务地址。
- `WorkflowDocument` 统一承载领域工作流、元数据和节点位置；画布、序列化和执行共享该文档。
- 反序列化通过节点注册表和工厂构造节点，不依赖无参反射构造。
- Windows 使用 CurrentUser DPAPI 加密本地密文文件；macOS 使用 `security` CLI；Linux 使用 `secret-tool` CLI。平台 CLI 调用不得记录密钥参数或标准输入。
- 撤销历史使用 `ICommand.Execute()` 与 `ICommand.Undo()`；一次拖拽、批量删除等复合操作各形成一个历史项。
- `csv-summarize-with-llm.ffw` 使用文本数据源读取含 CSV 内容和总结指令的提示文件，保持强类型端口且不引入隐式转换。

## Consequences

- Positive: 工作流文件可移植且不含 API Key；运行时节点可通过依赖注入安全创建；画布状态不会与执行图漂移。
- Positive: Chat Completions 可复用同一传输层，并兼容 DeepSeek 的 OpenAI 风格接口。
- Negative: macOS CLI 写入时密钥仍会短暂进入子进程参数；该风险在 v0.1 通过最小依赖实现接受，并在日志中严格脱敏。
- Trade-offs: macOS 和 Linux 的真实密钥库烟测需要对应操作系统；Windows CI 仅验证适配器契约和 DPAPI 实现。
