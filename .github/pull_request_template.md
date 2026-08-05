## 变更摘要

<!-- 用几句话说明本 PR 实际改变了什么，以及对应的 Stage/checklist。 -->

## 变更内容

- 

## 验证结果

- [ ] `dotnet build FlowForge.sln --configuration Release` 通过
- [ ] `dotnet test FlowForge.sln --configuration Release` 通过
- [ ] 如涉及分析器，`-p:EnforceCodeStyleInBuild=true -warnaserror` 通过
- [ ] 如涉及样例或 schema，已执行真实文件加载/集成路径
- [ ] 如涉及性能，已提供带 commit hash 的原始 JSON 和截图

## 安全与兼容性

- [ ] 未提交 API Key、令牌、私有路径或调试输出
- [ ] 未破坏 `INode`、`IPort` 或现有 TypeId
- [ ] schema 不兼容变更已增加版本迁移或附 ADR

## Stage Checklist

- [ ] 本 PR 中每个 checklist 项都有独立 commit
- [ ] commit 使用 Conventional Commits，并在正文引用 Stage/checklist
- [ ] 文档、样例、测试和配置与实际实现保持一致

## 备注

<!-- 记录未验证项、已知限制、迁移说明或需要审查者重点关注的内容。 -->
