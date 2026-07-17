using System.Text.Json.Nodes;

namespace FlowForge.Core.Serialization;

/// <summary>
/// 表示将工作流 JSON 文档从一个 schema 版本升级到下一个版本的迁移。
/// </summary>
public interface IWorkflowMigration
{
    /// <summary>
    /// 获取迁移前的 schema 版本。
    /// </summary>
    int FromVersion { get; }

    /// <summary>
    /// 获取迁移后的 schema 版本。
    /// </summary>
    int ToVersion { get; }

    /// <summary>
    /// 升级工作流 JSON 根对象。
    /// </summary>
    /// <param name="document">待升级的根对象。</param>
    /// <returns>升级后的根对象。</returns>
    JsonObject Migrate(JsonObject document);
}

/// <summary>
/// 按版本顺序执行工作流文档迁移。
/// </summary>
public sealed class WorkflowMigrator
{
    private readonly Dictionary<int, IWorkflowMigration> _migrations;

    /// <summary>
    /// 初始化迁移器。
    /// </summary>
    /// <param name="migrations">可用的单步迁移集合。</param>
    public WorkflowMigrator(IEnumerable<IWorkflowMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        _migrations = migrations.ToDictionary(migration => migration.FromVersion);
    }

    /// <summary>
    /// 将文档升级到当前 schema 版本。
    /// </summary>
    /// <param name="document">待升级的 JSON 根对象。</param>
    /// <returns>升级后的 JSON 根对象。</returns>
    public JsonObject Upgrade(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var version = document["schemaVersion"]?.GetValue<int>() ?? 0;
        while (version < 1)
        {
            if (!_migrations.TryGetValue(version, out var migration))
            {
                throw new NotSupportedException($"缺少从 schema 版本 {version} 开始的迁移。");
            }

            document = migration.Migrate(document);
            version = migration.ToVersion;
        }

        if (version != 1)
        {
            throw new NotSupportedException($"不支持 schema 版本 {version}。");
        }

        return document;
    }
}

/// <summary>
/// 将未标记版本的初始工作流文档升级为 schema v1。
/// </summary>
public sealed class V0ToV1WorkflowMigration : IWorkflowMigration
{
    /// <inheritdoc />
    public int FromVersion => 0;

    /// <inheritdoc />
    public int ToVersion => 1;

    /// <inheritdoc />
    public JsonObject Migrate(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document["schemaVersion"] = ToVersion;
        return document;
    }
}
