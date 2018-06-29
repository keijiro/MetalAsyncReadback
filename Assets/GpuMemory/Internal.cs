using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Klak.GpuMemory
{
    // Internal buffer class that handles a readback operations
    internal class ReadbackBuffer : System.IDisposable
    {
        #region Public properties

        public bool IsCompleted {
            get { return _buffer[0] != 0; }
        }

        public NativeSlice<int> Data {
            get { return new NativeSlice<int>(_buffer, 1); }
        }

        #endregion

        #region Private variables

        NativeArray<MetalAsyncReadbackPlugin.EventArgs> _args;
        NativeArray<int> _buffer;

        #endregion

        #region Public constructor

        public ReadbackBuffer(ComputeBuffer source, CommandBuffer commandBuffer)
        {
            _args = new NativeArray<MetalAsyncReadbackPlugin.EventArgs>(
                1, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );

            _buffer = new NativeArray<int>(
                source.count + 1, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory
            );

            unsafe {
                _args[0] = new MetalAsyncReadbackPlugin.EventArgs {
                    source = source.GetNativeBufferPtr(),
                    destination = (System.IntPtr)_buffer.GetUnsafePtr(),
                    lengthInBytes = source.stride * source.count
                };
            }

            commandBuffer.Clear();

            unsafe {
                commandBuffer.IssuePluginEventAndData(
                    MetalAsyncReadbackPlugin.GetCallback(),
                    0, (System.IntPtr)_args.GetUnsafePtr()
                );
            }

            Graphics.ExecuteCommandBuffer(commandBuffer);

            _buffer[0] = 0;
        }

        #endregion

        #region IDisposable Implementation

        bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Synchronize with the memcpy tasks before disposing the resources.
                MetalAsyncReadbackPlugin.WaitTasks();

                // Dispose all the unmanaged resources.
                _args.Dispose();
                _buffer.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}
