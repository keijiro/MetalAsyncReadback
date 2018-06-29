using System.Runtime.InteropServices;

namespace Klak.GpuMemory
{
    // Plugin entry points for MetalAsyncReadback
    internal static class MetalAsyncReadbackPlugin
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct EventArgs
        {
            public System.IntPtr source;
            public System.IntPtr destination;
            public int lengthInBytes;
        }

        #if !UNITY_EDITOR && UNITY_IOS
        const string _dllName = "__Internal";
        #else
        const string _dllName = "MetalAsyncReadback";
        #endif

        [DllImport(_dllName, EntryPoint = "MetalAsyncReadback_GetCallback")]
        static public extern System.IntPtr GetCallback();

        [DllImport(_dllName, EntryPoint = "MetalAsyncReadback_WaitTasks")]
        static public extern void WaitTasks();
    }
}
