# Stage 4 实施设计

## 目标

实现跨平台密钥存储、LLM 节点、`.ffw` 序列化、文件菜单和可撤销编辑，使工作流可安全保存、重新打开并继续编辑。

## 已确定的决策

- `.ffw` 只保存 `apiKeySecretId`，绝不保存 API Key 明文。
- OpenAI 与 DeepSeek 均使用 Chat Completions 协议；OpenAI 支持自定义 `baseUrl`，DeepSeek 使用固定服务地址。
- `WorkflowDocument` 是画布、持久化和执行共用的文档模型，节点 ViewModel 包装真实 `INode`。
- `csv-summarize-with-llm.ffw` 使用文本数据源读取包含 CSV 内容和总结指令的提示文件，不引入隐式类型转换。
- macOS Keychain 和 Linux Secret Service 使用系统 CLI 适配层，进程执行器必须可替换且不得记录密钥。
- 每次拖拽形成一个撤销历史项；删除节点会级联删除关联边并作为一个历史项撤销。

## 验证边界

- 单元和集成测试不得访问真实 LLM 网络或使用真实 API Key。
- Windows 上验证 DPAPI 真实读写；macOS 和 Linux 通过可替换适配器验证协议和错误映射，真实系统烟测留待多平台 CI。
- 每个 checklist 提交均执行定向测试、Release 全仓构建和全仓测试，然后立即推送。
