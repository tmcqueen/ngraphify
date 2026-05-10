using System.Reflection;
using System.Runtime.InteropServices;

namespace Ngraphiphy.Cluster;

public static class NativeLibraryResolver
{
    private static bool _registered = false;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolver);
    }

    private static IntPtr Resolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "leiden_interop")
            return IntPtr.Zero;

        // Search locations in priority order
        var locations = new[]
        {
            // Relative to executing assembly
            Path.Combine(AppContext.BaseDirectory, "libleiden_interop.so"),
            Path.Combine(AppContext.BaseDirectory, "native", "libleiden_interop.so"),
            // Absolute path from known build location (worktree)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "native", "build", "lib", "libleiden_interop.so"),
            // Absolute fallback - main repo build
            "/home/timm/ngraphiphy/native/build/lib/libleiden_interop.so",
            // Absolute fallback - task21 worktree build
            "/home/timm/ngraphiphy-task21/native/build/lib/libleiden_interop.so",
            // Try common system paths
            "/usr/local/lib/libleiden_interop.so",
            "/usr/lib/libleiden_interop.so",
        };

        foreach (var loc in locations)
        {
            var fullPath = Path.GetFullPath(loc);
            if (File.Exists(fullPath))
            {
                if (NativeLibrary.TryLoad(fullPath, out var handle))
                    return handle;
            }
        }

        // Try by name (system library path)
        if (NativeLibrary.TryLoad("libleiden_interop", assembly, searchPath, out var h))
            return h;

        return IntPtr.Zero;
    }
}
