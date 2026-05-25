using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RepoBar.Core.Storage;

public enum CredentialStoreMode
{
    Auto,
    File,
    WindowsCredentialManager,
}

public enum CredentialStoreKind
{
    File,
    WindowsCredentialManager,
}

public sealed record CredentialRecord(string Service, string Account, string Secret);

public interface ICredentialStore
{
    CredentialStoreKind Kind { get; }

    Task SaveAsync(CredentialRecord credential, CancellationToken cancellationToken = default);

    Task<CredentialRecord?> ReadAsync(string service, string account, CancellationToken cancellationToken = default);

    Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default);
}

public static class CredentialStoreFactory
{
    public static ICredentialStore Create(
        CredentialStoreMode mode,
        RepoBarPaths paths,
        IEnvironmentReader environment,
        bool isReleaseBuild,
        bool isWindows)
    {
        string? overrideValue = environment.GetVariable("REPOBAR_TOKEN_STORE");
        if (TryParseMode(overrideValue, out CredentialStoreMode overrideMode))
        {
            mode = overrideMode;
        }

        if (mode == CredentialStoreMode.File || (!isReleaseBuild && mode == CredentialStoreMode.Auto))
        {
            return new FileCredentialStore(paths.DebugAuthDirectory);
        }

        if ((mode == CredentialStoreMode.WindowsCredentialManager || mode == CredentialStoreMode.Auto) && isWindows)
        {
            return new WindowsCredentialStore();
        }

        if (mode == CredentialStoreMode.WindowsCredentialManager)
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only available on Windows.");
        }

        return new FileCredentialStore(paths.DebugAuthDirectory);
    }

    private static bool TryParseMode(string? value, out CredentialStoreMode mode)
    {
        mode = CredentialStoreMode.Auto;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Equals("file", StringComparison.OrdinalIgnoreCase)
            || value.Equals("disk", StringComparison.OrdinalIgnoreCase))
        {
            mode = CredentialStoreMode.File;
            return true;
        }

        if (value.Equals("credential-manager", StringComparison.OrdinalIgnoreCase)
            || value.Equals("windows", StringComparison.OrdinalIgnoreCase)
            || value.Equals("keychain", StringComparison.OrdinalIgnoreCase))
        {
            mode = CredentialStoreMode.WindowsCredentialManager;
            return true;
        }

        throw new InvalidOperationException($"Unsupported REPOBAR_TOKEN_STORE value '{value}'.");
    }
}

public sealed class FileCredentialStore(string directory) : ICredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public CredentialStoreKind Kind => CredentialStoreKind.File;

    public async Task SaveAsync(CredentialRecord credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        Directory.CreateDirectory(directory);
        string path = PathFor(credential.Service, credential.Account);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, credential, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        TryRestrictPermissions(path);
    }

    public async Task<CredentialRecord?> ReadAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        string path = PathFor(service, account);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CredentialRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        string path = PathFor(service, account);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string service, string account)
    {
        string fileName = Convert.ToHexString(Encoding.UTF8.GetBytes($"{service}:{account}")).ToLowerInvariant();
        return Path.Combine(directory, $"{fileName}.json");
    }

    private static void TryRestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}

public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistLocalMachine = 2;

    public CredentialStoreKind Kind => CredentialStoreKind.WindowsCredentialManager;

    public Task SaveAsync(CredentialRecord credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();

        string target = TargetName(credential.Service, credential.Account);
        byte[] secretBytes = Encoding.Unicode.GetBytes(credential.Secret);
        IntPtr secretPtr = Marshal.AllocCoTaskMem(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, secretPtr, secretBytes.Length);
            NativeCredential native = new()
            {
                Type = CredentialTypeGeneric,
                TargetName = target,
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = secretPtr,
                Persist = CredentialPersistLocalMachine,
                UserName = credential.Account,
            };

            if (!CredWrite(ref native, 0))
            {
                throw CreateWin32Exception("CredWrite");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(secretPtr);
        }

        return Task.CompletedTask;
    }

    public Task<CredentialRecord?> ReadAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();

        string target = TargetName(service, account);
        if (!CredRead(target, CredentialTypeGeneric, 0, out IntPtr credentialPtr))
        {
            int error = Marshal.GetLastPInvokeError();
            const int credentialNotFound = 1168;
            if (error == credentialNotFound)
            {
                return Task.FromResult<CredentialRecord?>(null);
            }

            throw CreateWin32Exception("CredRead", error);
        }

        try
        {
            NativeCredential native = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            byte[] secretBytes = new byte[native.CredentialBlobSize];
            Marshal.Copy(native.CredentialBlob, secretBytes, 0, secretBytes.Length);
            string secret = Encoding.Unicode.GetString(secretBytes);
            string userName = native.UserName ?? account;
            return Task.FromResult<CredentialRecord?>(new CredentialRecord(service, userName, secret));
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();

        string target = TargetName(service, account);
        if (!CredDelete(target, CredentialTypeGeneric, 0))
        {
            int error = Marshal.GetLastPInvokeError();
            const int credentialNotFound = 1168;
            if (error != credentialNotFound)
            {
                throw CreateWin32Exception("CredDelete", error);
            }
        }

        return Task.CompletedTask;
    }

    private static string TargetName(string service, string account) => $"RepoBar/{service}/{account}";

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only available on Windows.");
        }
    }

    private static System.ComponentModel.Win32Exception CreateWin32Exception(string operation) =>
        CreateWin32Exception(operation, Marshal.GetLastPInvokeError());

    private static System.ComponentModel.Win32Exception CreateWin32Exception(string operation, int error) =>
        new(error, $"{operation} failed.");

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential userCredential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string? TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
