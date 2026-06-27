using System.Reflection;

namespace DwsimPy.Runtime;

public static class RuntimeInfo
{
    public static string AssemblyName => typeof(RuntimeInfo).Assembly.GetName().Name ?? "DwsimPy.Runtime";

    public static string? TargetFramework =>
        typeof(RuntimeInfo).Assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
            ?.FrameworkName;
}
