using System.Text;

namespace FlowForge.Core.Tests.Nodes;

internal sealed class TemporaryFile : IAsyncDisposable
{
    private TemporaryFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static async Task<TemporaryFile> CreateAsync(string content, Encoding? encoding = null)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"flowforge-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(path, content, encoding ?? new UTF8Encoding(false));
        return new TemporaryFile(path);
    }

    public ValueTask DisposeAsync()
    {
        File.Delete(Path);
        return ValueTask.CompletedTask;
    }
}
