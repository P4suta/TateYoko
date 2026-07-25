#if TATEYOKO_PORTABLE
using System.Runtime.CompilerServices;

namespace TateYoko.App.Infrastructure;

internal static class PortableRuntimeBootstrap
{
    private const string RuntimeBaseDirectoryVariable =
        "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY";

    [ModuleInitializer]
    internal static void Initialize()
    {
        // A portable WinUI single-file publish extracts its signed, bundled runtime before loading
        // this assembly. Force registration-free WinRT activation to that exact directory instead
        // of letting process or user environment state redirect native runtime lookup.
        string runtimeBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        Environment.SetEnvironmentVariable(
            RuntimeBaseDirectoryVariable,
            runtimeBaseDirectory,
            EnvironmentVariableTarget.Process
        );
    }
}
#endif
