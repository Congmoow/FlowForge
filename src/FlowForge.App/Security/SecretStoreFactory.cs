using FlowForge.Core.Abstractions;

namespace FlowForge.App.Security;

/// <summary>
/// 为当前操作系统创建默认密钥存储。
/// </summary>
public static class SecretStoreFactory
{
    /// <summary>
    /// 创建当前操作系统支持的默认密钥存储实现。
    /// </summary>
    /// <returns>当前平台对应的密钥存储。</returns>
    /// <exception cref="PlatformNotSupportedException">当前操作系统没有受支持的密钥存储实现。</exception>
    public static ISecretStore CreateDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsDpapiSecretStore();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsKeychainSecretStore();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxSecretServiceSecretStore();
        }

        throw new PlatformNotSupportedException("当前操作系统没有受支持的 FlowForge 密钥存储实现。");
    }
}
