# Stage 5 实施设计

## 目标

在保持 Avalonia 11.2.3、.NET 8、ReactiveUI、System.Text.Json、Channel 和现有
工作流协议兼容性的前提下，完成 Stage 5 性能与工程化能力：画布视口导航与
剔除、真实 retained dirty scene、可重复压力场景与基准、开发模式性能 HUD、
剩余内置节点、统一节点目录、反射属性面板、跨平台插件加载和性能证据文档。

当前基线为 `origin/main` / `0b28c93` / `v0.1.0-alpha.4`。实现必须保持现有
Stage 1-4 行为兼容；NodeViewModel 包装真实 `INode`，NodeRegistry 是 Toolbox、
序列化、运行、属性面板和插件的共同节点目录。

## 已批准的决策

### 画布坐标、导航与剔除

- 新增 `CanvasViewport` 和 `ViewportTransform`，统一 world/view 坐标转换、
  逆变换命中测试和 0.25x-4x 缩放。
- Ctrl+滚轮以鼠标所在 world 点为缩放中心；空格+左键拖拽或中键拖拽平移。
- 网格根据缩放自适应密度，所有节点、端口、边和草稿边使用同一变换。
- 节点和边剔除使用纯函数 world bounds。节点 bounds 包含边界相交；贝塞尔
  bounds 必须包含起点、终点、两个控制点，并按 stroke 宽度膨胀。

### Retained dirty scene

- 画布采用按节点、按边拆分的 `ICustomDrawOperation` retained scene。
- 每个 operation 持有不可变绘制快照、紧 bounds、稳定 `Equals`/`Dispose`；
  `FormattedText`、`Pen`、`Geometry` 等可缓存资源随 operation 生命周期管理。
- 节点移动只替换该节点及关联边 operation；旧、新 bounds 的 union 形成 dirty
  区域。以 Avalonia `IRenderer.SceneInvalidated.DirtyRect` 和
  `RendererDebugOverlays.DirtyRects` 作为证明，不用 PushClip 或 pending rect
  冒充增量重绘。

### 性能场景与验证

- App 提供确定性的 100/250/500/1000 节点 dev/perf 场景，包含自动平移、固定
  采样窗口和 JSON 输出；xUnit 只验证生成器、坐标转换、剔除、dirty operation
  和统计逻辑，不在无 UI 线程伪测 FPS。
- FPS HUD 使用 5 秒 warmup、随后 30 秒自动平移和 1 秒采样，显示 FPS、节点数、
  `WorkingSet64`，避免逐帧绑定风暴。
- BenchmarkDotNet 固定 0.15.8，Chart.js 固定 4.5.1；性能结论只来自真实
  Release 窗口采样，不创建假数据或假截图。

### 统一节点模型、配置和秘密

- `NodeDefinition` 描述 TypeId、标题、端口和 typed config factory；
  `NodeRegistry` 提供可枚举定义、`CreateDefault`、原子 `RegisterRange`，并保留
  既有 `Register/Create` 兼容 API。
- 新增公共 `PortFactory` 返回 `IPort<T>`，供内置节点和外部插件共享端口身份。
- 配置继续使用 immutable record；`ConfigFieldAttribute` 位于 Core 公共契约，
  形状兼容 `[ConfigField(Label="...", Editor="...")]`。属性编辑通过构造新的
  typed config，并经现有 `ChangePropertyCommand<INodeConfig>` 进入 Undo/Redo。
- Password editor 从不显示、日志记录或序列化明文；输入写入 `ISecretStore`，
  配置只保存稳定 secret reference。保存、撤销和错误路径都必须验证 `.ffw` 不含
  明文。
- 打开文件时先构建并校验完整临时 Workflow，成功后原子替换画布；未知 TypeId、
  坏边和配置错误都可见，失败不留下半应用状态。Run 执行当前画布 Workflow，
  移除硬编码 Stage 3 sample 作为运行路径。

### 插件隔离

- 插件必须显式实现 `INodePlugin` 并返回 `IReadOnlyList<NodeDefinition>`；不扫描
  任意无参 `INode`。
- `PluginLoader` 使用带 `AssemblyDependencyResolver` 的可卸载自定义
  `AssemblyLoadContext`。`FlowForge.Core` 始终由 Default context 提供，保证
  `INode`、`IPort` 和 `INodePlugin` 类型身份一致；插件 context 保持到应用关闭，
  v0.1 不做热更新。
- 按顶层 DLL 文件名排序扫描。单 DLL、`ReflectionTypeLoadException`、缺依赖和
  重复 TypeId 形成诊断但不阻断其他插件或应用启动；单个插件的定义注册原子化。

## 不变的边界

- 不引入 WPF、WinForms、MAUI、Web 技术栈、ORM 或第三方 IoC 容器；技术版本不升级。
- 不修改根 `AGENTS.md`、`CLAUDE.md` 或其 checklist 勾选状态。
- 不把 API Key 或密码写入仓库；新增文本使用 UTF-8 无 BOM。
- 每个 checklist 项至少一个独立 commit。每个 commit 前串行运行 Release build 和
  test，并立即 push；提交正文包含 Stage 和原 checklist 原文。
- 发现设计与实现存在不兼容时，先写 Proposed ADR；性能未达标时继续在对应 perf
  checklist 下优化并重测，不提前创建完成 PR。

## 验收证据

最终必须具备：全量 Release build/test、analyzer `-warnaserror`、Cobertura
ViewModels 唯一 `(filename,line)` 聚合不低于 93.08%、两个 BenchmarkDotNet filter、
插件 sample smoke、Release 1000 节点 30 秒自动平移真实 FPS/内存 JSON、FPS 不低于
55 且 `WorkingSet64` 小于 300MB、性能截图、两个 Accepted ADR、干净工作区和 UTF-8
无 BOM 检查。未执行的证据必须明确标记为未验证。
