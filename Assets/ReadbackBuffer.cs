using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

public class ReadbackBuffer : IDisposable
{
    #region Public members

    public ComputeBuffer Source { get { return _source; } }

    public bool IsCompleted { get { return _readBuffer[0] != 0; } }

    public NativeSlice<int> Data {
        get { return new NativeSlice<int>(_readBuffer, 1); }
    }

    unsafe public ReadbackBuffer(ComputeBuffer source)
    {
        _source = source;
        _readBuffer = new NativeArray<int>(source.count + 1, Allocator.Persistent);

        _copyArgs = GCHandle.Alloc(
            new CopyBufferEventArgs {
                source = source.GetNativeBufferPtr(),
                destination = (IntPtr)_readBuffer.GetUnsafePtr(),
                lengthInBytes = source.count * 4
            },
            GCHandleType.Pinned
        );

        _copyCommand = new CommandBuffer();
        _copyCommand.IssuePluginEventAndData(
            CopyBuffer_GetCallback(),
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

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Synchronize with the memcpy tasks before disposing the resources.
            CopyBuffer_WaitTasks();

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

    struct CopyBufferEventArgs
    {
        public IntPtr source;
        public IntPtr destination;
        public int lengthInBytes;
    }

    #if !UNITY_EDITOR && UNITY_IOS
    const string _dllName = "__Internal";
    #else
    const string _dllName = "CopyBuffer";
    #endif

    [DllImport(_dllName)]
    static extern IntPtr CopyBuffer_GetCallback();

    [DllImport(_dllName)]
    static extern void CopyBuffer_WaitTasks();

    #endregion
}
