# FlowForge - 项目开发规范（AGENTS.md / CLAUDE.md）

> 本文件是开发智能体（Claude Code / Codex CLI / Cursor / Aider 等）的开发指令书。所有决策已在此文档中固化，**不要在实现过程中重新讨论技术选型**，直接按本文档执行。如发现规范冲突或缺失，先在 `docs/decisions/` 下补一份 ADR，再继续编码。
>
> **文件命名约定**：本仓库根目录维护 `AGENTS.md` 与 `CLAUDE.md` 两份内容完全一致的文件（推荐用符号链接或仓库 CI 校验保持同步）。遵循 [agents.md](https://agents.md/) 规范的工具（Codex CLI 等）默认读 `AGENTS.md`；Claude Code 优先读 `CLAUDE.md`。任何 agent 都应将本文档视为唯一权威来源。

---

## 1. 项目定位

**FlowForge** 是一款**节点式可视化 AI 工作流编辑器**，桌面端原生应用。用户在画布上拖拽节点、连线，构建数据处理 + LLM 调用的工作流，点击运行后沿 DAG 异步执行。

**目标作品集场景**：求职作品，对标杭州猿通科技 DecisionLinnc 产品域。

**非目标**（明确不做）：
- 不做 Web 版，不做移动端
- 不做团队协作 / 实时多人编辑
- 不做云端工作流托管，所有执行在本地
- 不做插件市场，插件机制只做 DLL 反射加载即可

---

## 2. 技术栈（已固化，不要更换）

| 维度 | 选型 | 版本 |
|------|------|------|
| 框架 | Avalonia | 11.2.x |
| 运行时 | .NET | 8.0 LTS |
| MVVM | ReactiveUI | 19.x |
| 序列化 | System.Text.Json | 内置 |
| 异步流 | System.Threading.Channels | 内置 |
| HTTP | HttpClient + Polly | Polly 8.x |
| 测试 | xUnit + FluentAssertions | 最新稳定 |
| Mock | NSubstitute | 最新稳定 |
| Lint | .editorconfig + Roslyn analyzers | - |
| CI | GitHub Actions | - |

**禁止引入**：WPF、WinForms、MAUI、任何 Web 技术栈、任何 ORM、任何 IoC 容器（用 Microsoft.Extensions.DependencyInjection 即可）。

---

## 3. 目录结构

```
FlowForge/
├── src/
│   ├── FlowForge.Core/                # 核心领域层（无 UI 依赖）
│   │   ├── Graph/                     # DAG 模型：Node、Port、Edge、Workflow
│   │   ├── Execution/                 # 执行引擎：Scheduler、Channel 路由
│   │   ├── Nodes/                     # 内置节点实现
│   │   │   ├── DataSource/            # CsvReader、TextReader
│   │   │   ├── Llm/                   # OpenAiNode、DeepSeekNode
│   │   │   ├── Transform/             # JsonParse、TextConcat、RegexExtract
│   │   │   └── Sink/                  # FileWriter、ConsoleLog
│   │   ├── Serialization/             # 工作流 JSON schema + 迁移
│   │   ├── Plugin/                    # 插件加载器（反射 + AssemblyLoadContext）
│   │   └── Abstractions/              # INode、IPort、IExecutionContext 等接口
│   │
│   ├── FlowForge.App/                 # Avalonia 应用层
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── CanvasViewModel.cs
│   │   │   ├── NodeViewModel.cs
│   │   │   ├── EdgeViewModel.cs
│   │   │   ├── PortViewModel.cs
│   │   │   ├── PropertyPanelViewModel.cs
│   │   │   └── ToolboxViewModel.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── Canvas/
│   │   │   │   ├── NodeCanvas.axaml         # 自绘画布
│   │   │   │   ├── NodeView.axaml           # 节点控件
│   │   │   │   └── EdgeView.axaml           # 连线控件（贝塞尔）
│   │   │   ├── Toolbox/
│   │   │   └── PropertyPanel/
│   │   ├── Controls/                  # 自定义控件
│   │   │   └── BezierEdgeShape.cs     # 贝塞尔曲线 Geometry
│   │   ├── Services/                  # UI 层服务（对话框、文件选择）
│   │   ├── Commands/                  # Undo/Redo 命令实现
│   │   └── App.axaml
│   │
│   └── FlowForge.Plugins.Sample/      # 示例插件，验证插件机制
│
├── tests/
│   ├── FlowForge.Core.Tests/
│   ├── FlowForge.App.Tests/           # ViewModel 测试，禁止依赖 UI 线程
│   └── FlowForge.Integration.Tests/   # 端到端工作流执行测试
│
├── docs/
│   ├── architecture.md                # 架构总览 + Mermaid 图
│   ├── node-protocol.md               # 节点协议规范
│   ├── workflow-schema.md             # 工作流 JSON schema
│   ├── decisions/                     # ADR（架构决策记录）
│   │   ├── 0001-use-avalonia.md
│   │   ├── 0002-reactiveui-over-toolkit-mvvm.md
│   │   ├── 0003-channel-based-execution.md
│   │   └── ...
│   └── perf/                          # 性能基准与截图
│
├── samples/                           # 示例工作流 .ffw 文件
│   ├── csv-summarize-with-llm.ffw
│   └── log-extract-classify.ffw
│
├── .github/workflows/
│   └── ci.yml
├── .editorconfig
├── Directory.Build.props              # 统一编译选项
├── FlowForge.sln
├── README.md
└── CHANGELOG.md
```

---

## 4. 核心抽象（必须按此实现）

### 4.1 节点协议 `INode`

```csharp
namespace FlowForge.Core.Abstractions;

public interface INode
{
    /// <summary>稳定 ID，跨会话不变。</summary>
    Guid Id { get; }

    /// <summary>节点类型标识，例如 "core.llm.openai"，用于反序列化定位实现类。</summary>
    string TypeId { get; }

    /// <summary>声明的输入端口集合，节点构造时确定，运行时不可变。</summary>
    IReadOnlyList<IPort> Inputs { get; }

    /// <summary>声明的输出端口集合。</summary>
    IReadOnlyList<IPort> Outputs { get; }

    /// <summary>节点配置（属性面板编辑的内容），需可序列化为 JSON。</summary>
    INodeConfig Config { get; set; }

    /// <summary>
    /// 执行节点。从 ctx 拉取输入端口数据，写入输出端口。
    /// 必须支持 CancellationToken 协作式取消。
    /// </summary>
    ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct);
}
```

### 4.2 端口与数据流

- 端口是**强类型**的：`IPort<T>` 携带 `Type DataType { get; }`
- 端口连接时**必须类型兼容**（精确匹配或子类型，不做自动转换）
- 数据流通过 `Channel<T>` 实现，每条 Edge 持有一个 `Channel<object?>`，由调度器在节点执行前后管理生产/消费

### 4.3 执行引擎语义

- **拓扑排序**：基于 Kahn 算法，检测到环抛 `CyclicGraphException`
- **执行模式**：默认**流式**——节点输出端口一旦写入数据，下游可立即消费（不等节点完全结束）
- **错误处理**：单节点抛异常 → 整个工作流标记 Failed，已运行节点不回滚（v1 不做事务）
- **取消**：用户点 Stop → CancellationTokenSource 触发 → 所有节点应在 200ms 内停止

### 4.4 序列化格式（工作流文件 `.ffw`）

```json
{
  "schemaVersion": 1,
  "metadata": {
    "name": "示例工作流",
    "createdAt": "2026-06-10T12:00:00Z",
    "flowForgeVersion": "0.1.0"
  },
  "nodes": [
    {
      "id": "guid",
      "typeId": "core.datasource.csv",
      "position": { "x": 100, "y": 200 },
      "config": { /* 节点特定配置 */ }
    }
  ],
  "edges": [
    {
      "id": "guid",
      "sourceNodeId": "guid",
      "sourcePortId": "output",
      "targetNodeId": "guid",
      "targetPortId": "input"
    }
  ]
}
```

**版本迁移**：每次 schema 不兼容变更，`schemaVersion + 1`，并在 `Serialization/Migrations/` 下加迁移类。加载老版本必须能升级到当前版本。

---

## 5. UI 关键设计

### 5.1 画布 `NodeCanvas`

- 继承 `Control`，重写 `Render(DrawingContext)` 自绘
- **视口虚拟化**：仅渲染当前可见区域内的节点和连线（节点 >100 个时性能必须保持 60fps）
- **缩放**：支持 0.25x ~ 4x，Ctrl+滚轮缩放，鼠标位置为缩放中心
- **平移**：空格+拖拽 或 中键拖拽
- **网格背景**：缩放时网格密度自适应

### 5.2 连线交互

- 从输出端口按下鼠标 → 拖拽出虚拟连线（贝塞尔曲线跟随鼠标）→ 释放在兼容输入端口上完成连接
- 鼠标悬浮在端口时高亮，类型不兼容时显示红色禁止图标
- 一个输入端口最多 1 条连线，输出端口可以多条（fan-out）

### 5.3 撤销/重做

- 命令模式：`ICommand { Execute(); Undo(); }`
- 覆盖操作：添加/删除节点、添加/删除连线、移动节点、修改节点属性
- 撤销栈深度无限制，但内存占用 >100MB 时给提示
- Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z

### 5.4 属性面板

- 选中节点时右侧面板显示该节点的 `Config` 对应表单
- 表单通过反射 + 特性 `[ConfigField(Label="...", Editor="...")]` 自动生成
- 编辑器类型：`TextBox` / `NumericUpDown` / `ComboBox` / `FilePicker` / `MultilineText` / `Password`

---

## 6. 内置节点（v0.1 必须实现）

| TypeId | 名称 | 输入 | 输出 | 配置 |
|--------|------|------|------|------|
| `core.datasource.csv` | CSV 读取 | - | rows: IEnumerable<Dictionary<string,string>> | filePath, hasHeader, delimiter |
| `core.datasource.text` | 文本读取 | - | content: string | filePath, encoding |
| `core.transform.json-parse` | JSON 解析 | text: string | json: JsonElement | - |
| `core.transform.text-concat` | 文本拼接 | parts: string[] | result: string | separator |
| `core.transform.regex-extract` | 正则提取 | text: string | matches: string[] | pattern, group |
| `core.transform.foreach` | 遍历 | items: IEnumerable | item: object（流式） | - |
| `core.llm.openai` | OpenAI 调用 | prompt: string | response: string | apiKey, model, temperature, baseUrl |
| `core.llm.deepseek` | DeepSeek 调用 | prompt: string | response: string | apiKey, model, temperature |
| `core.sink.file-writer` | 写文件 | content: string | - | filePath, append |
| `core.sink.console` | 控制台输出 | value: object | - | - |

**API Key 管理**：用 Windows DPAPI / macOS Keychain / Linux Secret Service 加密存储，**绝不**明文写入 `.ffw` 文件。

---

## 7. 开发阶段与里程碑

### Git 提交要求（强制）

**每个 Stage 内的每一个 checklist 项 = 至少一个独立 commit**。不允许把多个 checklist 项打包成一个大 commit。

**每个 commit 必须**：
1. 通过 `dotnet build` —— 编译失败的 commit 一律不允许
2. 通过 `dotnet test` —— 测试红的 commit 一律不允许（极少数允许的例外见下）
3. 使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范
4. 在 commit body 中**引用对应的 checklist 项**

**Conventional Commits 类型映射**：

| 类型 | 用途 | 示例 |
|------|------|------|
| `feat` | 新功能、新节点、新 ViewModel | `feat(canvas): add bezier edge rendering` |
| `fix` | bug 修复 | `fix(execution): prevent deadlock on cancellation` |
| `refactor` | 重构（行为不变） | `refactor(core): extract IScheduler interface` |
| `perf` | 性能优化 | `perf(canvas): add viewport culling for nodes` |
| `test` | 仅加测试 | `test(graph): add cyclic detection cases` |
| `docs` | 文档（含 ADR、README） | `docs(adr): add ADR-0003 channel-based execution` |
| `chore` | 构建/CI/依赖 | `chore(ci): add windows matrix to actions` |
| `build` | 构建配置 | `build: pin Avalonia to 11.2.1` |
| `style` | 格式化（不改逻辑） | `style: apply editorconfig rules` |

**Commit message 格式**：

```
<type>(<scope>): <短描述，祈使句，<=60 字符>

<可选正文，解释为什么，不只是做了什么。每行 <=72 字符>

Stage: <stage 编号>
Checklist: <对应的 checklist 项原文>
Refs: #<issue编号>（如有）
```

**完整示例**：

```
feat(canvas): implement node drag interaction

Mouse-down on a node captures pointer and tracks delta to update
NodePosition through CanvasViewModel.MoveNodeCommand. Snapping to
8px grid is opt-in via View settings.

Stage: 2
Checklist: 实现 NodeViewModel、NodeView.axaml,节点可拖动
```

**允许的例外**：
- 当一个 checklist 项过大（如"实现执行引擎"），可以拆成多个 commit，但**不能合并**多个 checklist 项
- 重构时若重构步骤会让中间 commit 测试暂时变红，必须用 `git rebase -i` 在合并前压成一个绿测 commit，或在 commit message 加 `[WIP]` 前缀并在合入主干前清理掉

**分支策略**：
- 主干：`main`
- 每个 Stage 一个分支：`stage/1-skeleton`、`stage/2-nodes-and-edges`...
- Stage 完成后 PR 合入 `main`，合并方式用 **Rebase and merge**（保留每个 commit）或 **Squash** 仅在 commit 太碎时用
- **禁止** force push 到 `main`

**Push 时机**：
- 每完成一个 checklist 项就 push 到当前分支（小步推送，CI 早暴露问题）
- 不允许本地堆积超过 3 个未推送的 commit

**每个 Stage 结束时（在合 PR 之前）必须**：
1. `dotnet build && dotnet test` 全绿
2. 在 `CHANGELOG.md` 的 `[Unreleased]` 区段记录该 Stage 的变更摘要
3. 性能相关 Stage 在 `docs/perf/` 下保存当前截图（含 commit hash 命名）
4. PR 描述里贴该 Stage 的 checklist,逐项打勾
5. PR 合入后打 Git tag:`v0.1.0-alpha.{stage}`,tag message 总结该 Stage 完成项

> 每个 checklist 项 = 至少一个 commit。完成后立即 commit + push,再做下一项。

### Stage 1：骨架（Week 1）

分支:`stage/1-skeleton`

- [ ] 创建 solution、项目结构、`Directory.Build.props`、`.editorconfig` → `chore: bootstrap solution structure`
- [ ] 添加 `.gitignore`、`README.md` 占位、`CHANGELOG.md` 初版 → `docs: add initial README and CHANGELOG`
- [ ] 配置 Avalonia 模板与启动入口 → `feat(app): scaffold Avalonia application shell`
- [ ] 配置依赖注入(Microsoft.Extensions.DI)与 ReactiveUI → `feat(app): wire up DI container and ReactiveUI`
- [ ] 主窗口三栏布局(左 Toolbox / 中 Canvas / 右 PropertyPanel) → `feat(app): add three-pane main window layout`
- [ ] 画布上硬编码渲染一个矩形节点(非交互) → `feat(canvas): render placeholder node rectangle`
- [ ] 添加 GitHub Actions CI(build + test) → `chore(ci): add github actions build workflow`
- [ ] 启用 Roslyn analyzers + lint check → `chore(ci): enable roslyn analyzers and lint`
- [ ] 写 ADR-0001(选用 Avalonia)、ADR-0002(选用 ReactiveUI) → `docs(adr): add ADR-0001 and ADR-0002`
- **验收**:运行后能看到三栏窗口,画布上有一个静态节点矩形;CI 全绿
- **Tag**:`v0.1.0-alpha.1`

### Stage 2：节点与连线 UI（Week 2）

分支:`stage/2-nodes-and-edges`

- [ ] 实现 `NodeViewModel`(Reactive 属性、Position、Selected) → `feat(viewmodel): add NodeViewModel with reactive properties`
- [ ] 实现 `NodeView.axaml` 数据模板 → `feat(view): add NodeView template`
- [ ] 节点拖动交互(Pointer 事件 → ViewModel 命令) → `feat(canvas): implement node drag interaction`
- [ ] 实现 `PortViewModel` 与端口在节点边缘的定位 → `feat(viewmodel): add PortViewModel and port layout`
- [ ] 实现 `EdgeViewModel` → `feat(viewmodel): add EdgeViewModel`
- [ ] 实现 `BezierEdgeShape` 自绘贝塞尔曲线 → `feat(view): render edges as bezier curves`
- [ ] 端口拖拽创建连线(虚拟连线跟随鼠标) → `feat(canvas): drag-to-create edge from output port`
- [ ] 端口连接时类型校验(不兼容显示禁止图标) → `feat(canvas): validate port type compatibility on connect`
- [ ] Toolbox 拖拽到画布创建节点 → `feat(toolbox): drag node template onto canvas`
- [ ] 节点选中状态与多选(Ctrl/Shift) → `feat(canvas): support node selection and multi-select`
- [ ] `CanvasViewModel` 单元测试(≥15 个) → `test(viewmodel): add CanvasViewModel unit tests`
- [ ] `EdgeViewModel` / `PortViewModel` 单元测试 → `test(viewmodel): add edge and port tests`
- **验收**:能在画布上手动构建一个 3 节点的图,纯 UI 不执行;ViewModel 测试 ≥ 25 个
- **Tag**:`v0.1.0-alpha.2`

### Stage 3：执行引擎（Week 3）

分支:`stage/3-execution-engine`

- [ ] 实现 `INode`、`IPort`、`IExecutionContext` 接口 → `feat(core): define node and port abstractions`
- [ ] 实现 `Workflow` 模型 → `feat(core): add Workflow domain model`
- [ ] 实现拓扑排序(Kahn 算法) + `CyclicGraphException` → `feat(core): add topological sort with cycle detection`
- [ ] 拓扑排序单元测试(含环检测) → `test(core): add topological sort tests`
- [ ] 实现 `ChannelEdgeRouter`(基于 `Channel<T>` 的边路由) → `feat(execution): implement channel-based edge router`
- [ ] 实现 `Scheduler`(并发节点调度 + 取消) → `feat(execution): implement workflow scheduler`
- [ ] Scheduler 单元测试(含取消、异常传播) → `test(execution): add scheduler tests`
- [ ] 实现 `core.datasource.csv` 节点 → `feat(nodes): add csv data source node`
- [ ] 实现 `core.datasource.text` 节点 → `feat(nodes): add text data source node`
- [ ] 实现 `core.transform.json-parse` 节点 → `feat(nodes): add json parse node`
- [ ] 实现 `core.transform.text-concat` 节点 → `feat(nodes): add text concat node`
- [ ] 实现 `core.sink.console` 节点 → `feat(nodes): add console sink node`
- [ ] UI 添加运行按钮 + 节点四态高亮(idle/running/success/failed) → `feat(canvas): add run button and node state indicators`
- [ ] 端到端集成测试:`samples/csv-to-console.ffw` → `test(integration): verify csv-to-console workflow`
- [ ] 写 ADR-0003(选择 Channel 而非 Rx Subject) → `docs(adr): add ADR-0003 channel-based execution`
- **验收**:拖一个 CSV→TextConcat→Console 的工作流,点运行能在控制台看到输出
- **Tag**:`v0.1.0-alpha.3`

### Stage 4：LLM 节点 + 序列化 + 撤销（Week 4）

分支:`stage/4-llm-and-persistence`

- [ ] 抽象 `ISecretStore`(跨平台秘钥存储接口) → `feat(core): define ISecretStore abstraction`
- [ ] Windows DPAPI 实现 → `feat(security): add DPAPI secret store for windows`
- [ ] macOS Keychain 实现 → `feat(security): add keychain secret store for macos`
- [ ] Linux Secret Service 实现 → `feat(security): add secret service store for linux`
- [ ] `ISecretStore` 跨平台测试 → `test(security): add secret store contract tests`
- [ ] 实现 `core.llm.openai` 节点(含 Polly 重试 + 超时) → `feat(nodes): add openai llm node with retry`
- [ ] 实现 `core.llm.deepseek` 节点 → `feat(nodes): add deepseek llm node`
- [ ] LLM 节点单元测试(用 NSubstitute mock HttpClient) → `test(nodes): add llm node unit tests`
- [ ] 定义 `.ffw` JSON schema v1 → `feat(serialization): define workflow schema v1`
- [ ] 实现工作流序列化/反序列化 → `feat(serialization): implement workflow load and save`
- [ ] 实现 schema 迁移框架(v0→v1 占位) → `feat(serialization): add schema migration framework`
- [ ] 序列化单元测试(往返 + 迁移) → `test(serialization): add roundtrip and migration tests`
- [ ] 文件菜单:新建/打开/保存/另存为 → `feat(app): add file menu commands`
- [ ] 实现 `ICommand` / `Undo` / `Redo` 抽象 → `feat(commands): add command pattern with undo`
- [ ] 实现 7 类操作命令(增删节点、增删连线、移动、改属性、批量) → `feat(commands): implement node and edge commands`
- [ ] Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z 快捷键绑定 → `feat(app): bind undo redo keyboard shortcuts`
- [ ] Undo/Redo 单元测试 → `test(commands): add undo redo tests`
- [ ] 跑通 `samples/csv-summarize-with-llm.ffw` → `test(integration): verify csv summarize workflow`
- **验收**:可以保存工作流、关闭重开后继续编辑;Ctrl+Z 能撤销节点拖动和属性修改
- **Tag**:`v0.1.0-alpha.4`

### Stage 5：性能与工程化（Week 5）

分支:`stage/5-perf-and-engineering`

- [ ] 视口虚拟化:节点剔除算法 → `perf(canvas): cull off-screen nodes during render`
- [ ] 视口虚拟化:连线剔除 → `perf(canvas): cull off-screen edges during render`
- [ ] 增量重绘(只重绘 dirty 区域) → `perf(canvas): implement dirty region invalidation`
- [ ] 1000 节点压力测试场景生成器 → `test(perf): add 1000-node stress scenario`
- [ ] 添加 BenchmarkDotNet 项目 + 拓扑排序基准 → `test(perf): add benchmarkdotnet baseline`
- [ ] 边路由调度基准测试 → `test(perf): add scheduler benchmarks`
- [ ] 帧率监控 HUD(FPS / 节点数 / 内存) → `feat(app): add dev mode fps hud`
- [ ] 实现 transform 节点剩余项(foreach、regex-extract) → `feat(nodes): add foreach and regex extract nodes`
- [ ] 实现 sink 节点(file-writer) → `feat(nodes): add file writer sink node`
- [ ] 属性面板自动生成(反射 + `[ConfigField]`) → `feat(panel): auto-generate property editors via reflection`
- [ ] 属性面板编辑器:TextBox/Numeric/ComboBox/FilePicker/Password → `feat(panel): add config field editors`
- [ ] 提升 ViewModel 单测覆盖率 ≥ 70%(补齐缺失) → `test(viewmodel): increase coverage to 70 percent`
- [ ] 启用 `dotnet test --collect "XPlat Code Coverage"` 报告生成 → `chore(ci): generate coverage report in ci`
- [ ] 插件机制:`PluginLoader`(`AssemblyLoadContext` + 反射) → `feat(plugin): implement plugin loader`
- [ ] 创建 `FlowForge.Plugins.Sample` 项目示例 → `feat(plugin): add sample plugin project`
- [ ] 主程序启动时扫描 plugins 目录 → `feat(app): auto-load plugins from plugins directory`
- [ ] 性能数据生成:节点数 vs FPS 折线图(HTML + Chart.js) → `docs(perf): add fps vs node count chart`
- [ ] 写 ADR-0004(视口虚拟化策略)、ADR-0005(插件加载隔离) → `docs(adr): add ADR-0004 and ADR-0005`
- **验收**:1000 节点画布缩放/平移帧率 ≥ 55fps;插件 DLL 放入 plugins 目录后自动加载
- **Tag**:`v0.1.0-alpha.5`

### Stage 6：打磨与发布（Week 6）

分支:`stage/6-polish-and-release`

- [ ] 录制 30 秒 GIF demo,放入 `docs/assets/` → `docs: add demo gif`
- [ ] README 重写:卖点、架构图(Mermaid)、快速开始 → `docs: rewrite readme with architecture diagram`
- [ ] 写 `docs/architecture.md`(分层 + 数据流 + 时序图) → `docs: add architecture overview`
- [ ] 写 `docs/node-protocol.md` → `docs: add node protocol spec`
- [ ] 写 `docs/workflow-schema.md` → `docs: add workflow schema spec`
- [ ] 补齐 ADR-0006 性能预算策略 → `docs(adr): add ADR-0006 performance budget`
- [ ] 添加示例工作流 `samples/log-extract-classify.ffw` → `docs: add log extract sample`
- [ ] 添加示例工作流 `samples/csv-to-console.ffw`(若 Stage3 未提交) → `docs: add csv to console sample`
- [ ] GitHub Actions 三平台构建 matrix(Win/Mac/Linux) → `chore(ci): add multi-platform build matrix`
- [ ] Release 流程:tag 触发自动打包并发布产物 → `chore(ci): add release workflow on tag`
- [ ] 启用 GitHub Issues 模板(bug、feature) → `chore: add github issue templates`
- [ ] 启用 PR 模板 → `chore: add pull request template`
- [ ] README 末尾"What I learned"段落 → `docs: add what i learned section`
- [ ] 录制 demo 视频并上传 README → `docs: add demo video link`
- [ ] 最终性能截图归档 → `docs(perf): archive final performance snapshots`
- [ ] 发布 v0.1.0 正式版 → `chore: release v0.1.0`
- **验收**:clone 仓库后 `dotnet run` 能在 Win/Mac/Linux 任一平台启动;Release 页面有三平台二进制
- **Tag**:`v0.1.0`(正式版,不再加 alpha 后缀)

---

## 8. 编码规范

### 8.1 命名

- **公共 API** 必须有 XML 文档注释
- 接口以 `I` 开头，抽象类不强制
- ViewModel 后缀 `ViewModel`，View 后缀 `View`（除主窗口）
- 测试方法命名：`MethodName_Scenario_ExpectedBehavior`

### 8.2 异步

- 所有 IO 操作必须 `async`，方法名以 `Async` 结尾
- 默认接受 `CancellationToken` 参数，没有理由就传 `default`
- 不允许 `async void`，唯一例外是事件处理器
- 不允许 `.Result` / `.Wait()`，必要时用 `await`

### 8.3 错误处理

- 领域错误用自定义异常（`CyclicGraphException`、`PortTypeMismatchException` 等）
- 不要 catch `Exception` 然后吞掉，必须至少 log
- UI 层捕获到未处理异常 → 弹错误对话框 + 写日志，不崩溃

### 8.4 不变性

- ViewModel 的可观察属性用 `[Reactive]`（ReactiveUI.Fody）
- Core 层的领域模型尽量用 `record` 或不可变类
- 集合返回 `IReadOnlyList<T>` / `IReadOnlyDictionary<K,V>`，不暴露 `List<T>`

### 8.5 测试

- 每个 PR 不允许降低覆盖率
- ViewModel 测试**禁止**依赖 UI 线程，必须能在 CI 中无头运行
- 集成测试用 `FlowForge.Integration.Tests`，加载真实 `.ffw` 跑端到端

---

## 9. 性能预算（硬指标）

| 指标 | 目标 | 测量方式 |
|------|------|----------|
| 启动时间（冷启动到主窗口可交互） | < 1.5s | Stopwatch in `App.OnFrameworkInitializationCompleted` |
| 1000 节点画布平移 FPS | ≥ 55 | 自带 HUD |
| 工作流加载（100 节点 .ffw 文件） | < 200ms | 单元测试 + Stopwatch |
| 内存（1000 节点空闲态） | < 300MB | dotMemory / Process.WorkingSet64 |
| LLM 节点首次响应延迟 | < 500ms（不含网络） | 节点内 Stopwatch |

任何 PR 导致以上指标退化 > 10% 必须先优化。

---

## 10. 文档要求

### README.md 必须包含

1. 顶部 30 秒 demo GIF
2. 一句话定位 + 三个 bullet 卖点
3. 架构图（Mermaid，三层：View / ViewModel / Core）
4. 快速开始（3 行命令）
5. 关键技术决策摘要（链到 ADR）
6. 性能数据（含截图）
7. 路线图（v0.2 / v0.3 计划）
8. 协议（MIT）

### docs/architecture.md 必须包含

- 整体分层图
- 数据流图（用户操作 → 命令 → ViewModel → Core → 持久化）
- 执行引擎时序图（Mermaid sequenceDiagram）
- 插件加载流程

### ADR 模板

```markdown
# ADR-XXXX: 标题

**Status**: Accepted | Deprecated | Superseded by ADR-YYYY
**Date**: 2026-MM-DD

## Context
（为什么需要做这个决策？）

## Decision
（决策是什么？）

## Consequences
- Positive: ...
- Negative: ...
- Trade-offs: ...
```

---

## 11. Claude Code 协作约定

**每次开始新任务前**：
1. 先读 `CHANGELOG.md` 了解当前进度
2. 检查 `TaskList`,找到当前 Stage 的下一个未完成项
3. 不要跳阶段,按 Stage 1 → 6 顺序推进
4. 确认当前在正确的 stage 分支(`git branch --show-current` 应为 `stage/<N>-...`)

**做每一个 checklist 项的标准流程**:

1. 读对应 checklist 项,理解范围
2. 写代码 + 测试
3. 本地 `dotnet build && dotnet test` 必须全绿
4. `git add` 仅该项相关文件(不允许夹带其他改动)
5. `git commit` 用第 7 节定义的 Conventional Commits 格式
6. `git push` 到当前 stage 分支
7. 把该 checklist 项标记完成

**严禁的 commit 反模式**:

- ❌ 一个 commit 包含 2+ 个 checklist 项
- ❌ commit message 写 "wip"、"update"、"fix"、"misc" 等无信息词
- ❌ 编译失败或测试红的 commit 推到远程
- ❌ commit 中夹带格式化、重命名等无关改动(应单独 `style:` commit)
- ❌ 把秘钥、调试输出、注释掉的代码提交进去
- ❌ 跳过 push 直接堆 3+ 个本地 commit

**每次完成一个 Stage 前**:
1. 跑 `dotnet build && dotnet test`,全绿才算完成
2. 更新 `CHANGELOG.md` 的 `[Unreleased]` 区段
3. 在 `docs/perf/` 下记录性能截图(如该 Stage 涉及性能)
4. 在 GitHub 上发起 PR:`stage/<N>-...` → `main`
5. PR 描述里贴该 Stage 的完整 checklist,逐项打勾并附 commit hash 链接
6. PR 通过后 **Rebase and merge**(保留每个 commit)
7. 在 `main` 上打 tag:`v0.1.0-alpha.{stage}`(Stage 6 例外,直接 `v0.1.0`)
8. 在本地切回 `main` 并拉取,然后创建下一阶段分支

**遇到歧义时**:
- 优先看本文档第 4 节(核心抽象)
- 再看相关 ADR
- 都没有 → 在 `docs/decisions/` 下新建 ADR 草稿(Status: Proposed),先用 `docs(adr): propose ADR-XXXX ...` 提交,实现后改为 Accepted 再单独 commit

**禁止行为**:
- 禁止引入本文第 2 节"禁止引入"列表中的依赖
- 禁止跳过测试直接合并
- 禁止把秘钥、API Key 写入仓库(用 `appsettings.Development.json.example` 占位)
- 禁止破坏性改动 `INode` / `IPort` 接口而不增加 schemaVersion
- 禁止 force push 到 `main` 或已合并的 stage 分支
- 禁止 `git rebase -i` 修改已 push 到共享分支的历史(本地未 push 的分支允许 rebase 整理)

---

## 12. 求职作品集附加要求

本项目最终交付物**面向面试官**，所以以下额外要求必须满足：

1. **README 顶部** 必须有项目截图 + GIF demo
2. **commit history 干净**：每个 commit message 用 [Conventional Commits](https://www.conventionalcommits.org/) 规范
3. **GitHub repo**：开启 Actions、Issues、Discussions
4. **Releases** 页面提供 Win/Mac/Linux 三平台二进制
5. **关键决策必须有 ADR**：让面试官能看到你的工程思考过程
6. **README 末尾** 写一段"What I learned"，2-3 段，讲清楚你解决的最难的 3 个技术问题（比如 Channel 流式调度、视口虚拟化、Avalonia 自绘性能）

---

**最后说明**：本文档是**契约**。如果你（Claude Code）在执行中发现某条规范不合理，**先停下来写 ADR 提议变更**，由用户审阅后再继续，不要擅自偏离。
