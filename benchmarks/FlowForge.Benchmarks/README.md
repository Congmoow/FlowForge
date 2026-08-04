# FlowForge 性能基准

此项目使用固定版本 BenchmarkDotNet 0.15.8，测量核心工作流算法和调度路径。
基准只使用内存中的确定性图与节点，不访问文件、网络或控制台。

## 运行拓扑排序基准

在仓库根目录执行：

```powershell
dotnet run --project benchmarks/FlowForge.Benchmarks/FlowForge.Benchmarks.csproj `
  --configuration Release -- --filter "*TopologicalSortBenchmarks*" `
  --artifacts artifacts/benchmarks
```

`TopologicalSortBenchmarks` 使用 100 和 1000 个节点两个参数。图在
`GlobalSetup` 中构造，基准方法只调用真实的 `TopologicalSorter.Sort`。报告与临时
产物统一写入仓库根目录的 `artifacts/benchmarks`，该目录不纳入版本控制。

## 运行调度基准

在仓库根目录执行：

```powershell
dotnet run --project benchmarks/FlowForge.Benchmarks/FlowForge.Benchmarks.csproj `
  --configuration Release -- --filter "*WorkflowSchedulerBenchmarks*" `
  --artifacts artifacts/benchmarks
```

`WorkflowSchedulerBenchmarks` 使用 10、100 和 1000 个节点三个参数。每次迭代在
`IterationSetup` 中创建新的线性工作流，基准方法调用真实的
`WorkflowScheduler.ExecuteAsync`，通过 `ChannelEdgeRouter` 在相邻节点间传递一个
内存值。
