# Stage 5 实施计划

## 执行规则

- 基线：`stage/5-perf-and-engineering` 从 `origin/main` 的 `0b28c93` 创建。
- 每个任务先按 TDD 写最小失败测试，确认失败原因正确，再写最小实现并回归。
- 每个任务只 stage 自己的文件；提交前顺序固定为：
  `dotnet build FlowForge.sln --configuration Release --no-restore --disable-build-servers --maxcpucount:1`，
  然后 `dotnet test FlowForge.sln --configuration Release --no-build --no-restore --disable-build-servers --maxcpucount:1`。
- 每项通过后立即以英文 Conventional Commit 标题、英中双语正文提交并 push；不得
  把两个 checklist 项合并到一个提交。
- 执行按每 3 项为一批报告实际 commit、push、全量 build/test、定向测试和专项证据。

## 0. 计划与 ADR 前置提交

### 0.1 Stage 5 实施设计（补充 checklist）

- 文件：`docs/plans/2026-08-03-stage5-design.md`、本计划。
- 失败测试：不新增代码测试；验证文件存在、UTF-8 无 BOM、内容覆盖已批准设计。
- 实现：记录坐标/剔除、retained dirty scene、性能场景、统一节点目录、配置秘密、
  插件隔离和最终证据边界。
- 提交：`docs(plan): add Stage 5 implementation design`；正文末尾写
  `Stage: 5` 与 `Checklist: Stage 5 实施设计（补充）`。

### 0.2 Proposed 性能与插件 ADR

- 文件：`docs/decisions/0004-viewport-virtualization.md`、
  `docs/decisions/0005-plugin-loading-isolation.md`。
- 失败测试：Markdown 模板/状态/决策边界检查。
- 实现：先以 `Proposed` 记录待实现的视口和插件合同，不提前声称已验收。
- 提交：`docs(adr): propose Stage 5 performance and plugin contracts`；引用两个
  原 Stage 5 checklist，单独 build/test/push。

## A. 画布与性能

### A1. 补充：画布视口导航

- 文件：新增 `src/FlowForge.App/Canvas/CanvasViewport.cs`、
  `src/FlowForge.App/Canvas/ViewportTransform.cs`；调整
  `src/FlowForge.App/Controls/NodeCanvas.cs`、相关 AXAML；测试放入
  `tests/FlowForge.App.Tests/Canvas/`。
- 失败测试：缩放范围、鼠标中心不变、world/view 往返、负坐标命中、空格左拖、
  中键平移和网格密度随 zoom 变化。
- 实现：统一变换对象和逆变换命中，输入事件只更新 Viewport 状态，保留旧 API。
- 验证/提交：定向 `dotnet test ... --filter FullyQualifiedName~Viewport`，再全量；
  `feat(canvas): add viewport navigation`，逐项 push。

### A2. 节点剔除

- 文件：新增 `src/FlowForge.App/Canvas/CanvasCulling.cs`（纯函数 bounds/相交）；
  调整 `NodeCanvas.cs`；测试 `tests/FlowForge.App.Tests/Canvas/CanvasCullingTests.cs`。
- 失败测试：负坐标、部分相交、边界相切、不同 zoom/pan，节点 world bounds 不依赖
  UI 线程。
- 实现：先把 view bounds 逆变换为 world bounds，再保留边界相交节点。
- 验证/提交：定向 culling 测试、Release 全量；
  `perf(canvas): cull off-screen nodes during render`，逐项 push。

### A3. 边剔除

- 文件：新增 `src/FlowForge.App/Canvas/BezierBounds.cs`；调整
  `BezierEdgeShape.cs`、`NodeCanvas.cs`；测试 `tests/FlowForge.App.Tests/Canvas/BezierBoundsTests.cs`。
- 失败测试：控制点穿越视口、端点和控制点完整包含、stroke margin、边界相切及
  `Edges.CollectionChanged` 后绘制集合更新。
- 实现：复用现有控制点规则计算完整贝塞尔包围盒并按 stroke 膨胀，订阅/退订边集合。
- 验证/提交：定向 edge culling/collection 测试、Release 全量；
  `perf(canvas): cull off-screen edges during render`，逐项 push。

### A4. 增量 dirty region

- 文件：新增 `src/FlowForge.App/Canvas/RetainedCanvasScene.cs`、
  `NodeDrawOperation.cs`、`EdgeDrawOperation.cs`、`CanvasDrawSnapshot.cs`；调整
  `NodeCanvas.cs`、`BezierEdgeShape.cs`；测试 `tests/FlowForge.App.Tests/Canvas/DirtyRegionTests.cs`。
- 失败测试：operation 不可变快照、紧 bounds、稳定 Equals/Dispose、缓存资源、旧/新
  bounds union、节点移动只替换自身和关联边、节点/边订阅退订，以及单节点变化的
  `SceneInvalidated.DirtyRect` 小于整画布。
- 实现：以每节点/每边 `ICustomDrawOperation` 建 retained scene，使用 renderer
  invalidation/`RendererDebugOverlays.DirtyRects` 取证；不以 clip/pending rect 代替。
- 验证/提交：Avalonia 画布定向测试和真实 renderer smoke、Release 全量；
  `perf(canvas): implement dirty region invalidation`，逐项 push。

### A5. 1000 节点压力场景

- 文件：新增 `src/FlowForge.App/Diagnostics/StressScenarioGenerator.cs`、
  `PerfSamplingOptions.cs`、`PerfSample.cs`、`PerfScenarioRunner.cs`；调整
  `Program.cs`/`App.axaml.cs` 或启动服务；测试 `tests/FlowForge.App.Tests/Performance/`。
- 失败测试：100/250/500/1000 节点数量、固定 seed、坐标和边确定性、采样统计、
  JSON schema、取消和无人值守选项。
- 实现：App 可启动 dev/perf 场景，自动平移并输出真实 FPS/WorkingSet JSON；xUnit
  不伪测 FPS。
- 验证/提交：生成器/统计定向测试、Release 全量；
  `test(perf): add 1000-node stress scenario`，逐项 push。

### A6. TopologicalSort BenchmarkDotNet

- 文件：新增 `benchmarks/FlowForge.Benchmarks/FlowForge.Benchmarks.csproj`、
  `Program.cs`、`TopologicalSortBenchmarks.cs`、`README.md`；调整
  `FlowForge.sln` 添加 benchmarks solution folder；输出固定 `artifacts/benchmarks/`。
- 失败测试：项目为 net8 Exe、BenchmarkDotNet 0.15.8、Params 100/1000、GlobalSetup
  只构图、无真实 IO。
- 实现：构造稳定 DAG，基准只测 `TopologicalSorter.Sort`。
- 验证/提交：Release build；BenchmarkDotNet filter `TopologicalSortBenchmarks`；
  `test(perf): add benchmarkdotnet baseline`，逐项 push。

### A7. Scheduler/Channel BenchmarkDotNet

- 文件：同 `benchmarks/FlowForge.Benchmarks/` 新增
  `WorkflowSchedulerBenchmarks.cs` 及最小 benchmark node/context fixture。
- 失败测试：Params 10/100/1000、async scheduler/channel 路径、无文件/网络/控制台、
  每次迭代隔离 Workflow。
- 实现：基于真实 `WorkflowScheduler` 和 `ChannelEdgeRouter` 的内存节点基准。
- 验证/提交：Release build；BenchmarkDotNet filter `WorkflowSchedulerBenchmarks`；
  `test(perf): add scheduler benchmarks`，逐项 push。

### A8. Dev FPS HUD

- 文件：新增 `src/FlowForge.App/Diagnostics/PerfHudViewModel.cs`、
  `PerfHudControl.cs`、`PerfRunOptions.cs`；调整 `MainWindow.axaml`、
  `MainWindowViewModel.cs`、`App.axaml.cs`；测试 `tests/FlowForge.App.Tests/Performance/`。
- 失败测试：1 秒采样、5 秒 warmup、30 秒自动平移、FPS/节点数/WorkingSet64 字段、
  无逐帧绑定风暴、JSON 文件取消和错误路径。
- 实现：HUD 只在 dev/perf 模式显示，采样与 UI 更新节流，保留可重复命令入口。
- 验证/提交：统计/配置定向测试、Release 全量；
  `feat(app): add dev mode fps hud`，逐项 push。

## B. 节点与统一模型

### B1. Foreach 与 Regex 节点

- 文件：新增 `src/FlowForge.Core/Nodes/Transform/ForeachNode.cs`、
  `RegexExtractNode.cs` 及 immutable configs；测试放
  `tests/FlowForge.Core.Tests/Nodes/Transform/`。
- 失败测试：Foreach `IEnumerable` 到 `object` 流式输出、null 空流、逐项取消；Regex
  CultureInvariant、1 秒 timeout、无匹配空数组、无效 pattern/group、取消。
- 实现：复用 `IExecutionContext`/Channel 读写，不引入自动转换或同步等待。
- 验证/提交：节点定向测试、Release 全量；
  `feat(nodes): add foreach and regex extract nodes`，逐项 push。

### B2. FileWriter sink

- 文件：新增 `src/FlowForge.Core/Nodes/Sink/FileWriterSinkNode.cs` 及 config；测试
  `tests/FlowForge.Core.Tests/Nodes/Sink/FileWriterSinkNodeTests.cs`。
- 失败测试：`ReadAllAsync<string>` 多值、执行期只打开一次、append=false 单次截断、
  append=true 追加、原文无自动换行、UTF-8 无 BOM、目录/权限/取消。
- 实现：单次打开目标流，逐项写原文，协作取消并正确释放资源。
- 验证/提交：文件 sink 定向测试、Release 全量；
  `feat(nodes): add file writer sink node`，逐项 push。

### B3. Core 节点目录契约

- 文件：新增 `src/FlowForge.Core/Abstractions/NodeDefinition.cs`、
  `PortFactory.cs`、`INodePlugin.cs`；调整 `Serialization/NodeRegistry.cs`、
  `Nodes/Internal/NodePort.cs` 及全部内置节点注册入口；测试
  `tests/FlowForge.Core.Tests/Serialization/NodeRegistryTests.cs`、
  `tests/FlowForge.Core.Tests/Abstractions/NodeCatalogTests.cs`。
- 失败测试：可枚举 definition、CreateDefault、原子 RegisterRange、重复 TypeId 不
  部分注册、旧 Register/Create 兼容、公共 `PortFactory` 返回正确 `IPort<T>`，全部
  Stage 3/4 内置 TypeId 可创建。
- 实现：以 definition 统一标题/端口/config factory，保持旧 serializer 调用兼容。
- 验证/提交：节点目录定向测试、Release 全量；
  `refactor(core): expose node catalog contracts`，逐项 push。

### B4. App 绑定真实 Workflow

- 文件：调整 `CanvasViewModel.cs`、`NodeViewModel.cs`、`EdgeViewModel.cs`、
  `ToolboxViewModel.cs`、`MainWindowViewModel.cs`、`AvaloniaWorkflowFileService.cs`、
  `Stage3SampleWorkflowRunner.cs`/接口、DI 和相关 AXAML；新增 workflow mapper；测试
  `tests/FlowForge.App.Tests/Workflow/` 与 Integration tests。
- 失败测试：NodeViewModel 包装 INode、definitions 驱动 Toolbox、打开临时构建失败
  时不污染画布、未知 TypeId/坏边可见、typed config 保存、Run 执行当前画布 Workflow、
  不再硬编码 Stage 3 sample。
- 实现：NodeRegistry 贯通 Toolbox/打开/保存/运行/属性面板，成功后原子应用状态。
- 验证/提交：App/Integration 定向测试、Release 全量；
  `feat(app): bind canvas to real workflow nodes`，逐项 push。

## C. 属性面板与覆盖率

### C1. 反射生成属性编辑器

- 文件：新增 `src/FlowForge.Core/Abstractions/ConfigFieldAttribute.cs`、
  `src/FlowForge.App/PropertyPanel/ConfigDescriptor.cs`、
  `PropertyPanelViewModel.cs`；调整全部 config record、`CanvasEditCommands.cs`、
  `MainWindowViewModel.cs`；测试 `tests/FlowForge.App.Tests/PropertyPanel/`。
- 失败测试：六类 editor 元数据、immutable record descriptor、选中同步、typed
  config replacement、ChangePropertyCommand Undo/Redo、未知属性和校验错误。
- 实现：Core 只提供公共 attribute；App 反射 descriptor 按定义渲染并构造新 config。
- 验证/提交：PropertyPanel 定向测试、Release 全量；
  `feat(panel): auto-generate property editors via reflection`，逐项 push。

### C2. 六类配置编辑器与秘密接线

- 文件：新增/调整 `src/FlowForge.App/Views/PropertyPanel/` AXAML、controls、
  `Services/IFilePickerService.cs`、secret adapter、绑定；测试
  `tests/FlowForge.App.Tests/PropertyPanel/`。
- 失败测试：TextBox/NumericUpDown/ComboBox/FilePicker/MultilineText/Password 类型
  转换、校验、取消、异常；Password 不显示/日志/序列化明文，保存/撤销失败仍只留
  stable secret reference。
- 实现：FilePicker 和 ISecretStore 由 DI 提供，Password editor 从不回填明文。
- 验证/提交：PropertyPanel + serialization/security 定向测试、Release 全量；
  `feat(panel): add config field editors`，逐项 push。

### C3. ViewModel 覆盖率补齐

- 文件：先生成 Cobertura，再只修改 `tests/FlowForge.App.Tests/` 中真实缺口对应的
  测试；必要时不改生产代码。
- 失败测试：以唯一 `(filename,line)` 聚合的 ViewModels 行覆盖率低于父任务基线时，
  先定位未覆盖真实分支，不用无意义断言凑数。
- 实现：补齐真实交互、错误和取消路径，最终聚合不低于 93.08%。
- 验证/提交：`dotnet test --collect "XPlat Code Coverage"` 与覆盖率报告；
  `test(viewmodel): increase coverage to 70 percent`，提交标题保持合同原文，逐项 push。

### C4. CI Cobertura gate

- 文件：新增 `coverage.runsettings`、`eng/verify-viewmodel-coverage.ps1`；调整
  `.github/workflows/ci.yml`。
- 失败测试：脚本对所有 Cobertura 文件唯一聚合 `(filename,line)`，阈值 93.08，缺
  报告/重复/低于阈值均失败；CI 生成并上传 Cobertura artifact。
- 实现：固定路径、UTF-8 输出、清晰错误码，避免把测试总覆盖率误当 ViewModels 覆盖率。
- 验证/提交：本地脚本 gate、CI YAML 结构检查、Release 全量；
  `chore(ci): generate coverage report in ci`，逐项 push。

## D. 插件与文档

### D1. PluginLoader

- 文件：新增 `src/FlowForge.Core/Plugin/PluginLoader.cs`、
  `PluginLoadContext.cs`、`PluginDiagnostic.cs`、`INodePlugin.cs`（若未在 B3 建立）；
  测试 `tests/FlowForge.Core.Tests/Plugin/` 及测试 fixture/plugin 项目。
- 失败测试：有效插件、损坏 DLL、缺依赖、`ReflectionTypeLoadException`、重复 TypeId、
  `FlowForge.Core` Default context 类型身份、单插件原子注册、context 卸载。
- 实现：文件名排序扫描顶层 DLL，ALC + AssemblyDependencyResolver，诊断收集不阻断
  其他插件，插件 context 保持到显式关闭。
- 验证/提交：Plugin 定向测试、Release 全量；
  `feat(plugin): implement plugin loader`，逐项 push。

### D2. Sample plugin

- 文件：新增 `src/FlowForge.Plugins.Sample/` 项目、uppercase node/config/plugin；
  调整 `FlowForge.sln`；测试/ smoke fixture 放 Core tests 或 Integration tests，不能
  复制 Core DLL。
- 失败测试：`core.transform.uppercase` 中文标题/端口、构建引用同一 Core、Loader 加载
  后得到 definition。
- 实现：显式 `INodePlugin` 返回一项 `NodeDefinition`，保留可卸载边界。
- 验证/提交：solution Release build、plugin loader smoke、全量 test；
  `feat(plugin): add sample plugin project`，逐项 push。

### D3. App 自动加载插件

- 文件：调整 `App.axaml.cs`、`Program.cs`、DI、`ToolboxViewModel.cs`、文件服务、
  属性面板和运行 mapper；新增 App plugin service；测试
  `tests/FlowForge.App.Tests/Plugin/`。
- 失败测试：`AppContext.BaseDirectory/plugins` 无目录为空成功、异步启动错误写
  Trace/状态、插件 definition 贯通 Toolbox/打开保存/属性/运行、应用关闭释放 context。
- 实现：启动扫描不阻塞 UI，错误不使应用退出，关闭集中 Dispose loader contexts。
- 验证/提交：App plugin smoke、Release 全量；
  `feat(app): auto-load plugins from plugins directory`，逐项 push。

### D4. 性能图表与真实证据

- 文件：新增 `docs/perf/fps-vs-node-count.html`、真实 JSON/表格和
  `docs/perf/*-1000-nodes.*` 截图；必要时新增采样说明。
- 失败条件：同窗口尺寸、100/250/500/1000 节点各 5 秒 warmup + 30 秒自动平移，
  真实 JSON 可追溯到被测 commit；1000 节点平均 FPS >=55，WorkingSet64 <300MB。
- 实现：Chart.js CDN 精确 4.5.1，提供静态 fallback；记录硬件、OS、GPU、SDK、
  测量口径和 Benchmark 摘要。不达标则回到 A4/A5/A8 优化重测。
- 验证/提交：Release 实窗采样、HTML 静态检查、截图 hash 检查；
  `docs(perf): add fps vs node count chart`，逐项 push。

### D5. 接受性能/插件 ADR

- 文件：更新 `docs/decisions/0004-viewport-virtualization.md`、
  `docs/decisions/0005-plugin-loading-isolation.md`。
- 失败条件：没有真实实现/测量依据时不得改 Accepted。
- 实现：记录 Avalonia 无 `InvalidateVisual(Rect)`、retained operation 边界、插件
  失败诊断和卸载策略，引用实现文件和真实证据。
- 验证/提交：ADR 引用路径检查；`docs(adr): accept ADR-0004 and ADR-0005`，逐项 push。

### D6. Stage 5 文档收口

- 文件：`CHANGELOG.md` [Unreleased]、`README.md` 当前能力/真实性能证据；
  `AGENTS.md` 与 `CLAUDE.md` 不改。
- 失败条件：文档不得写入未经运行的 FPS、内存、覆盖率或 benchmark 数字。
- 实现：记录 Stage 5 变更、测量入口和已验证/未验证边界。
- 验证/提交：Markdown/UTF-8 无 BOM 检查、最终 Release build/test；
  `docs: update changelog for stage 5`，逐项 push。

## 最终收口顺序

1. `dotnet restore FlowForge.sln`。
2. Release build；再以 `-warnaserror` 构建 analyzer gate。
3. 全量 Release tests；Cobertura gate >=93.08。
4. 两个 BenchmarkDotNet filter 和插件 sample smoke。
5. Release 1000 节点自动平移 30 秒，确认真实 FPS >=55、WorkingSet64 <300MB，
   保存 JSON 和截图。
6. 检查 git status clean、AGENTS/CLAUDE 不变、全部文本 UTF-8 无 BOM。
7. 生成只基于真实 diff/命令的中英双语 PR 描述，创建 ready PR 到 `main`；等待
   GitHub Actions 绿后使用 Rebase and merge。
8. 合并后 fetch `origin/main`，在合并后的 main commit 创建并 push annotated tag
   `v0.1.0-alpha.5`。只有 `D:\Desktop\FlowForge` 仍是干净 main 时才 fast-forward
   pull；否则停止并报告，不触碰主工作区。
