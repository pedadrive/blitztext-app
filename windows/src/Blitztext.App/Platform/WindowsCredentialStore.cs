using System.Runtime.InteropServices;
using System.Text;
using Blitztext.Core.Abstractions;

namespace Blitztext.App.Platform;

/// <summary>
/// Secure credential storage using the Windows Credential Manager (generic credentials,
/// DPAPI-protected per user). This is the Windows equivalent of the macOS Keychain used
/// by <c>KeychainService</c>.
/// </summary>
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string TargetPrefix = "Blitztext:";

    public void Save(CredentialKey key, string value)
    {
        var blob = Encoding.Unicode.GetBytes(value);
        var blobHandle = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobHandle, blob.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = TargetName(key),
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobHandle,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = key.ToString()
            };

            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException(
                    $"Zugangsdaten konnten nicht gespeichert werden (Win32 {Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            Marshal.FreeHGlobal(blobHandle);
        }
    }

    public string? Load(CredentialKey key)
    {
        if (!CredRead(TargetName(key), CRED_TYPE_GENERIC, 0, out var handle))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(handle);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                return null;

            var blob = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, blob, 0, (int)cred.CredentialBlobSize);
            var value = Encoding.Unicode.GetString(blob);
            return string.IsNullOrEmpty(value) ? null : value;
        }
        finally
        {
            CredFree(handle);
        }
    }

    public void Delete(CredentialKey key) => CredDelete(TargetName(key), CRED_TYPE_GENERIC, 0);

    public bool HasValue(CredentialKey key) => !string.IsNullOrEmpty(Load(key));

    private static string TargetName(CredentialKey key) => TargetPrefix + key;

    // --- Win32 interop (advapi32) ---

    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredWriteW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredReadW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
