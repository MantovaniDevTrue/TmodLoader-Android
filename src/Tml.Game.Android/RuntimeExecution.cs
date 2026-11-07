using System.Reflection;
using System.Runtime.InteropServices;

namespace Tml.Mobile.Android;

internal static class RuntimeExecution
{
    [DllImport("libmonosgen-2.0.so")] private static extern IntPtr mono_domain_get();
    [DllImport("libmonosgen-2.0.so")] private static extern IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr address);
    [DllImport("libmonosgen-2.0.so")] private static extern int mono_jit_info_get_code_size(IntPtr info);
    internal static void Report(MethodInfo executed)
    {
        IntPtr info = mono_jit_info_table_find(mono_domain_get(), executed.MethodHandle.GetFunctionPointer());
        GameEntry.Log(info == IntPtr.Zero
            ? "ANDROID_CPU_EXECUTION arch=" + RuntimeInformation.ProcessArchitecture + " jit-lookup=unconfirmed"
            : $"ANDROID_CPU_EXECUTION arch={RuntimeInformation.ProcessArchitecture} mode=mono-jit code-bytes={mono_jit_info_get_code_size(info)}");
    }
}
