# FlowForge 节点协议

本文档描述内置节点和插件节点共同遵守的 Core 协议。节点协议位于 `FlowForge.Core`，不依赖 Avalonia；画布、属性面板、序列化和执行引擎都通过这些稳定抽象使用节点。

## 1. 节点生命周期

一个节点由 `NodeDefinition` 创建，并由 `NodeRegistry` 按稳定 `TypeId` 管理。节点实例在工作流中必须具有稳定的 `Guid Id`；输入和输出端口在构造时确定，运行期间不得改变。

```csharp
public interface INode
{
    Guid Id { get; }
    string TypeId { get; }
    IReadOnlyList<IPort> Inputs { get; }
    IReadOnlyList<IPort> Outputs { get; }
    INodeConfig Config { get; set; }
    ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct);
}
```

调度器会为工作流中的节点启动执行任务。节点应在 `ExecuteAsync` 中完成以下工作：

1. 使用上下文从声明的输入端口读取数据。
2. 处理单值或异步数据流，并在产生结果时写入声明的输出端口。
3. 在文件、网络和 LLM 调用等 IO 边界传递 `CancellationToken`。
4. 正常返回表示节点成功；抛出异常表示节点失败，不应吞掉错误。

节点不负责创建下游 Channel，也不负责决定全局失败传播；这些语义由 `WorkflowScheduler` 和 `ChannelEdgeRouter` 统一管理。

## 2. 端口和类型规则

端口使用稳定的节点内标识、展示名称和运行时数据类型描述：

```csharp
public interface IPort
{
    string Id { get; }
    string Name { get; }
    Type DataType { get; }
}

public interface IPort<T> : IPort;
```

连接一条边时，工作流先确认源端口是输出、目标端口是输入，再执行：

```csharp
targetPort.DataType.IsAssignableFrom(sourcePort.DataType)
```

因此，连接支持精确匹配或目标类型可接收源类型的赋值兼容；协议不提供隐式字符串转换、JSON 转换或集合转换。一个输入端口最多接受一条边，输出端口可以 fan-out 到多条边。

执行上下文只允许节点访问自己声明的端口，并提供单值读取、完整流读取和单值写入：

```csharp
public interface IExecutionContext
{
    ValueTask<T?> ReadAsync<T>(IPort<T> port, CancellationToken ct);
    IAsyncEnumerable<T?> ReadAllAsync<T>(IPort<T> port, CancellationToken ct);
    ValueTask WriteAsync<T>(IPort<T> port, T? value, CancellationToken ct);
}
```

每条工作流边拥有一个独立的 `Channel<object?>`。写入 fan-out 输出时，路由器会把同一个值写入该输出端口关联的每条边；下游读取时再按端口声明类型校验值，不匹配会形成 `PortValueTypeMismatchException`。

## 3. 执行、完成、失败与取消

- `TopologicalSorter` 使用 Kahn 算法检测环；发现环时抛出 `CyclicGraphException`，不会开始执行。
- 调度器并行启动拓扑排序后的节点任务，节点可在上游仍然运行时消费已经写入的值。
- 节点正常结束后，路由器完成该节点的所有输出边，让下游流读取结束。
- 一个节点抛出异常后，工作流标记为失败，路由器向全部边传播异常并取消其他节点；已完成节点不回滚。
- 用户点击 Stop 时取消共享令牌。节点必须协作检查令牌，不能使用阻塞 `.Wait()` 或 `.Result`。
- 进度接收器通过 `NodeExecutionEvent` 接收 `Running`、`Succeeded` 和 `Failed` 状态；进度回调本身的异常由调度器记录，不改变节点执行结果。

## 4. 配置协议和属性面板

节点配置必须实现空标记接口 `INodeConfig`，推荐使用不可变 `record`。配置通过 `NodeDefinition.ConfigType` 描述，序列化时写入节点文档的 `config` 对象。

若配置属性需要出现在属性面板，可使用 `[ConfigField]` 声明展示元数据：

```csharp
public sealed record RegexExtractNodeConfig(
    [property: ConfigField(Label = "正则表达式", Editor = "MultilineText", Required = true, Order = 0)]
    string Pattern = "",
    [property: ConfigField(Label = "捕获组", Editor = "NumericUpDown", Min = 0, Order = 1)]
    int Group = 0) : INodeConfig;
```

当前支持的编辑器为 `TextBox`、`MultilineText`、`NumericUpDown`、`ComboBox`、`FilePicker` 和 `Password`。Password 编辑器只接收或显示密钥引用；实现不得把 API Key 明文放入 `Config`、日志或 `.ffw` 文件。

## 5. 内置节点目录

内置节点由 `NodeRegistry.CreateDefault()` 注册。下表的端口类型是协议中的运行时类型，不是 UI 显示文字。

| TypeId | 名称 | 输入端口 | 输出端口 | 配置 |
| --- | --- | --- | --- | --- |
| `core.datasource.csv` | CSV 读取 | — | `rows: IEnumerable<Dictionary<string,string>>` | `filePath`、`hasHeader`、`delimiter` |
| `core.datasource.text` | 文本读取 | — | `content: string` | `filePath`、`encoding` |
| `core.transform.json-parse` | JSON 解析 | `text: string` | `json: JsonElement` | 无 |
| `core.transform.text-concat` | 文本拼接 | `parts: string[]` | `result: string` | `separator` |
| `core.transform.foreach` | 遍历 | `items: IEnumerable` | `item: object` | 无 |
| `core.transform.regex-extract` | 正则提取 | `text: string` | `matches: string[]` | `pattern`、`group` |
| `core.llm.openai` | OpenAI 调用 | `prompt: string` | `response: string` | `apiKeySecretId`、`model`、`temperature`、`baseUrl` |
| `core.llm.deepseek` | DeepSeek 调用 | `prompt: string` | `response: string` | `apiKeySecretId`、`model`、`temperature` |
| `core.sink.file-writer` | 写文件 | `content: string` | — | `filePath`、`append` |
| `core.sink.console` | 控制台输出 | `value: object` | — | 无 |

节点配置字段的 JSON 命名遵守 `System.Text.Json` 的 camelCase 规则；端口 `Id` 和 `TypeId` 使用区分大小写的稳定字符串。新版本若改变已有字段的含义或结构，必须递增 schema 版本并添加迁移，而不是静默改变旧文件的解释方式。

## 6. 插件协议

插件程序集必须引用与主应用相同类型身份的 `FlowForge.Core`，并提供公共无参 `INodePlugin` 实现：

```csharp
public interface INodePlugin
{
    IReadOnlyList<NodeDefinition> Definitions { get; }
}
```

插件应使用 `NodeDefinition.Create<TConfig>()` 提供 TypeId、标题、端口、默认配置和节点工厂。`PluginLoader` 按 `plugins` 目录顶层 DLL 的文件名排序加载，每个插件使用独立可卸载 `AssemblyLoadContext`；重复 TypeId、缺少依赖、反射类型加载失败和注册异常会形成诊断，不会留下部分 definition。

插件节点与内置节点共享以下约束：

- TypeId 在全局 `NodeRegistry` 中唯一且跨会话稳定。
- 端口必须在 definition 中声明，不能在执行时偷偷增加端口。
- 配置必须实现 `INodeConfig` 并可由 `System.Text.Json` 序列化。
- ExecuteAsync 必须支持协作式取消，并通过上下文访问数据。
- 插件不能把密钥、调试输出或外部服务凭据写入工作流文件。

## 7. 最小实现清单

新增节点或插件前，至少确认：

- [ ] 有稳定的 TypeId、节点标题和 Guid Id。
- [ ] 输入/输出端口 Id 唯一，方向和运行时类型正确。
- [ ] Config 是可序列化的 `INodeConfig`，敏感信息使用 secret reference。
- [ ] ExecuteAsync 的每个 IO 路径都传递 CancellationToken。
- [ ] 输出完成、异常和取消路径不被吞掉。
- [ ] NodeRegistry/插件 definition 可以创建该节点。
- [ ] 增加 Core 单元测试；若涉及真实 `.ffw`，增加 Integration 测试。
