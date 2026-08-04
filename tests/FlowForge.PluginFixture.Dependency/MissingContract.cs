namespace FlowForge.PluginFixture.Dependency;

public class MissingBase
{
    protected MissingBase()
    {
    }
}

public sealed class MissingContract
{
    public static string Value => "fixture";
}
