# FlowForge 工作流文件规范

FlowForge 使用 `.ffw` 保存一个可编辑、可重新加载的工作流文档。文件是 UTF-8 JSON，属性名使用 `System.Text.Json` Web 默认的 camelCase 规则。当前版本为 `schemaVersion: 1`。

## 1. 根对象

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `schemaVersion` | `integer` | 是 | 文档结构版本，当前必须为 `1`。未标记版本的旧对象按 v0 迁移。 |
| `metadata` | object | 是 | 工作流名称、创建时间和 FlowForge 版本。 |
| `nodes` | array | 是 | 节点文档集合，节点 ID 必须唯一。 |
| `edges` | array | 是 | 端口连接集合，边 ID 必须唯一。 |

## 2. 元数据

```json
{
  "name": "CSV 到控制台",
  "createdAt": "2026-07-10T00:00:00Z",
  "flowForgeVersion": "0.1.0"
}
```

- `name` 是用户可见的工作流名称。
- `createdAt` 使用 ISO 8601 的带时区时间值。
- `flowForgeVersion` 记录生成该文档的应用版本，不决定 schema 迁移路径。

## 3. 节点对象

每个节点包含稳定实例 ID、节点类型 ID、画布位置和节点特定配置：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | UUID 字符串 | 节点跨会话稳定的实例标识。 |
| `typeId` | string | `NodeRegistry` 中的稳定节点类型标识，例如 `core.datasource.csv`。 |
| `position` | object | 画布 world 坐标，包含数值 `x`、`y`。 |
| `config` | object | 由该 `typeId` 对应的 `NodeDefinition.ConfigType` 解释的 JSON 对象。 |

示例：

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "typeId": "core.datasource.csv",
  "position": { "x": 96, "y": 80 },
  "config": {
    "filePath": "data/stage3-people.csv",
    "hasHeader": true,
    "delimiter": ","
  }
}
```

`position` 只描述编辑器位置，不参与节点执行。相对文件路径由应用根据工作流文件位置解析；配置中的路径不能借此绕过应用的文件访问边界。

## 4. 边对象

```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "sourceNodeId": "11111111-1111-1111-1111-111111111111",
  "sourcePortId": "rows",
  "targetNodeId": "22222222-2222-2222-2222-222222222222",
  "targetPortId": "value"
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | UUID 字符串 | 边的稳定实例标识。 |
| `sourceNodeId` | UUID 字符串 | 源节点 ID，必须存在于 `nodes`。 |
| `sourcePortId` | string | 源节点的输出端口 ID。 |
| `targetNodeId` | UUID 字符串 | 目标节点 ID，必须存在于 `nodes`。 |
| `targetPortId` | string | 目标节点的输入端口 ID。 |

加载时 `Workflow.AddEdge` 会校验节点引用、端口方向、数据类型兼容性和目标输入端口是否已有连接。源端口允许 fan-out；目标输入端口在 v1 中最多一条边。

## 5. 完整示例

```json
{
  "schemaVersion": 1,
  "metadata": {
    "name": "CSV 到控制台",
    "createdAt": "2026-07-10T00:00:00Z",
    "flowForgeVersion": "0.1.0"
  },
  "nodes": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "typeId": "core.datasource.csv",
      "position": { "x": 96, "y": 80 },
      "config": {
        "filePath": "data/stage3-people.csv",
        "hasHeader": true,
        "delimiter": ","
      }
    },
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "typeId": "core.sink.console",
      "position": { "x": 420, "y": 120 },
      "config": {}
    }
  ],
  "edges": [
    {
      "id": "33333333-3333-3333-3333-333333333333",
      "sourceNodeId": "11111111-1111-1111-1111-111111111111",
      "sourcePortId": "rows",
      "targetNodeId": "22222222-2222-2222-2222-222222222222",
      "targetPortId": "value"
    }
  ]
}
```

仓库中的可执行版本见 [`samples/csv-to-console.ffw`](../samples/csv-to-console.ffw)；带 LLM 的示例见 [`samples/csv-summarize-with-llm.ffw`](../samples/csv-summarize-with-llm.ffw)。

## 6. 序列化与加载流程

保存时，应用从当前 `Workflow` 和画布位置创建 `WorkflowDocument`，再由 `WorkflowSerializer.SaveAsync` 写入流。加载时依次执行：

1. 从 UTF-8 JSON 读取根对象。
2. 通过 `WorkflowMigrator` 将旧版本升级到当前版本。
3. 根据 `typeId` 从 `NodeRegistry` 获取 definition，并将 `config` 反序列化为强类型配置。
4. 创建节点并添加到临时 `Workflow`。
5. 按文件顺序校验并添加所有边。
6. 所有步骤成功后，应用才把临时工作流和画布位置替换为当前编辑状态。

未知 `schemaVersion`、未知 `typeId`、错误配置、缺少节点引用、端口方向错误、类型不兼容和重复输入连接都应作为可见错误处理，不能静默丢失节点或边。

## 7. 版本迁移

schema 不兼容变更必须递增 `schemaVersion`，并在 `FlowForge.Core/Serialization` 增加从前一版本到后一版本的 `IWorkflowMigration` 实现。迁移器从根对象的 `schemaVersion` 开始按版本顺序运行：

```text
v0（缺少 schemaVersion）
  │ V0ToV1WorkflowMigration
  ▼
v1（当前）
```

当前 v0→v1 迁移只补写 `schemaVersion: 1`。v1 的字段语义和节点 TypeId 已经被样例、往返测试和集成测试使用；改变已有字段含义时必须新增迁移和回归测试。

## 8. 安全约束

- `.ffw` 只保存不透明的 `apiKeySecretId`，不保存 OpenAI 或 DeepSeek API Key 明文。
- 密钥通过 `ISecretStore` 访问；平台实现使用 Windows DPAPI、macOS Keychain 或 Linux Secret Service。
- 日志、异常和示例文件不得包含真实密钥。
- `config` 允许保存模型、温度、服务地址和普通文件路径，但这些字段不会替代系统密钥存储。
- 提交工作流前应检查 JSON 内容、关联数据文件和 Git diff，确保没有本地密钥或私有路径。

## 9. 兼容性检查清单

- [ ] 根对象包含 `schemaVersion: 1`、`metadata`、`nodes` 和 `edges`。
- [ ] 每个节点和边的 ID 唯一，边引用的节点与端口真实存在。
- [ ] `typeId` 已由内置目录或插件注册。
- [ ] `config` 与节点 definition 的配置类型匹配。
- [ ] 连接的源输出与目标输入满足端口类型兼容规则。
- [ ] 文档为 UTF-8，无 API Key、令牌或机器私有密钥材料。
