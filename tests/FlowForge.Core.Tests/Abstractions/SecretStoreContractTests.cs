using System.Reflection;
using FlowForge.Core.Abstractions;
using FluentAssertions;

namespace FlowForge.Core.Tests.Abstractions;

public sealed class SecretStoreContractTests
{
    [Fact]
    public void ISecretStore_ExpectedMembers_ExposeAsynchronousSecretOperations()
    {
        var methods = typeof(ISecretStore).GetMethods(BindingFlags.Instance | BindingFlags.Public);

        methods.Should().ContainSingle(method =>
            method.Name == nameof(ISecretStore.GetSecretAsync)
            && method.ReturnType == typeof(ValueTask<string>)
            && HasSecretIdAndCancellationToken(method));
        methods.Should().ContainSingle(method =>
            method.Name == nameof(ISecretStore.SetSecretAsync)
            && method.ReturnType == typeof(ValueTask)
            && HasSecretIdValueAndCancellationToken(method));
        methods.Should().ContainSingle(method =>
            method.Name == nameof(ISecretStore.DeleteSecretAsync)
            && method.ReturnType == typeof(ValueTask<bool>)
            && HasSecretIdAndCancellationToken(method));
    }

    [Fact]
    public void SecretStoreException_Message_ExposesDomainFailure()
    {
        var exception = new SecretStoreException("密钥存储不可用。");

        exception.Should().BeAssignableTo<InvalidOperationException>();
        exception.Message.Should().Be("密钥存储不可用。");
    }

    private static bool HasSecretIdAndCancellationToken(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 2
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(CancellationToken);
    }

    private static bool HasSecretIdValueAndCancellationToken(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 3
            && parameters[0].ParameterType == typeof(string)
            && parameters[1].ParameterType == typeof(string)
            && parameters[2].ParameterType == typeof(CancellationToken);
    }
}
