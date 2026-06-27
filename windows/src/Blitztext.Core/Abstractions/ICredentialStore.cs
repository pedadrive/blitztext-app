namespace Blitztext.Core.Abstractions;

public enum CredentialKey
{
    OpenAiApiKey
}

/// <summary>
/// Secure credential storage. The Windows implementation uses the Windows Credential
/// Manager (DPAPI-backed), mirroring the macOS Keychain.
/// </summary>
public interface ICredentialStore
{
    void Save(CredentialKey key, string value);
    string? Load(CredentialKey key);
    void Delete(CredentialKey key);
    bool HasValue(CredentialKey key);
}

public static class CredentialStoreExtensions
{
    public static bool IsConfigured(this ICredentialStore store) =>
        store.HasValue(CredentialKey.OpenAiApiKey);
}
