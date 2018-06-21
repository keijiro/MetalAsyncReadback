// MetalAsyncReadback - Async GPU readback plugin for Unity
// https://github.com/keijiro/MetalAsyncReadback

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalAsyncReadback
{
    public sealed class ReadbackBuffer : IDisposable
    {
        #region Public members

        public ComputeBuffer Source { get { return _source; } }

        public bool IsCompleted { get { return _readBuffer[0] != 0; } }

        public NativeSlice<int> Data {
            get { return new NativeSlice<int>(_readBuffer, 1); }
        }

        public ReadbackBuffer(ComputeBuffer source)
        {
            _source = source;
            _readBuffer = new NativeArray<int>(source.count + 1, Allocator.Persistent);

            unsafe {
                _copyArgs = GCHandle.Alloc(
                    new ReadbackEventArgs {
                        source = source.GetNativeBufferPtr(),
                        destination = (IntPtr)_readBuffer.GetUnsafePtr(),
                        lengthInBytes = source.stride * source.count
                    },
                    GCHandleType.Pinned
                );
            }

            _copyCommand = new CommandBuffer();
            _copyCommand.IssuePluginEventAndData(
                MetalAsyncReadback_GetCallback(),
                0, _copyArgs.AddrOfPinnedObject()
            );
        }

        public void RequestReadback()
        {
            Graphics.ExecuteCommandBuffer(_copyCommand);
            _readBuffer[0] = 0;
        }

        #endregion

        #region Private members

        ComputeBuffer _source;
        NativeArray<int> _readBuffer;
        GCHandle _copyArgs;
        CommandBuffer _copyCommand;

        #endregion

        #region IDisposable Implementation

        bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Synchronize with the memcpy tasks before disposing the resources.
                MetalAsyncReadback_WaitTasks();

                // Dispose all the unmanaged resources.
                _readBuffer.Dispose();
                _copyArgs.Free();
                _copyCommand.Dispose();
                _copyCommand = null;
            }

            _disposed = true;
        }

        #endregion

        #region Native plugin interface

        [StructLayout(LayoutKind.Sequential)]
        struct ReadbackEventArgs
        {
            public IntPtr source;
            public IntPtr destination;
            public int lengthInBytes;
        }

        #if !UNITY_EDITOR && UNITY_IOS
        const string _dllName = "__Internal";
        #else
        const string _dllName = "MetalAsyncReadback";
        #endif

        [DllImport(_dllName)] static extern IntPtr MetalAsyncReadback_GetCallback();
        [DllImport(_dllName)] static extern void MetalAsyncReadback_WaitTasks();

        #endregion
    }
}
