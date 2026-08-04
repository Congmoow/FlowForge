# FlowForge BenchmarkDotNet 摘要

本文件保存 2026-08-04 实际生成的 BenchmarkDotNet 0.15.8 摘要，避免性能报告依赖被 `.gitignore` 排除的 `artifacts/` 原始目录。表格中的数字来自该次 Release Dry job；它们是算法和调度基准，不是画布 FPS 测量。

## 环境

- BenchmarkDotNet：0.15.8。
- OS：Windows 11，10.0.26200.8875，25H2/2025 Update/Hudson Valley 2。
- CPU：12th Gen Intel Core i5-12450H，8 physical / 12 logical cores。
- .NET SDK：8.0.302。
- .NET runtime：8.0.7，X64 RyuJIT x86-64-v3。
- RID：win-x64。

## 拓扑排序

基准类：`TopologicalSortBenchmarks.Sort`。`GlobalSetup` 只构造稳定 DAG，基准方法只调用拓扑排序。

| NodeCount | Mean | Error | StdDev | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 8.353 μs | 0.1345 μs | 0.1258 μs | 12.71 KB |
| 1000 | 84.122 μs | 1.4887 μs | 1.3925 μs | 124.7 KB |

## WorkflowScheduler / Channel

基准类：`WorkflowSchedulerBenchmarks.ExecuteAsync`。基准使用内存节点、真实 scheduler/channel 路径，不访问文件、网络或控制台。

| NodeCount | Mean | Error | StdDev | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 10 | 190.997 μs | 10.37 μs | 30.10 μs | 29.59 KB |
| 100 | 466.346 μs | 20.98 μs | 60.20 μs | 312.56 KB |
| 1000 | 4.495 ms | 270.35 μs | 730.92 μs | 3147.36 KB |

## 复核入口

使用 Release 构建后的 BenchmarkDotNet 项目，并分别按以下过滤器运行：

```text
TopologicalSortBenchmarks
WorkflowSchedulerBenchmarks
```

本摘要不替代原始 BenchmarkDotNet 输出；它把本次实际报告中的环境和关键结果固定在版本库中，便于离线审阅。
