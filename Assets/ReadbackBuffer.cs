using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

public class ReadbackBuffer : IDisposable
{
    #region External objects

    ComputeBuffer _sourceBuffer;

    #endregion

    #region Public methods

    NativeSlice<int> _exposedData;

    public NativeSlice<int> data { get { return _exposedData; } }

    public ReadbackBuffer(ComputeBuffer source)
    {
        _sourceBuffer = source;
    }

    public void Update()
    {
        while (_readbackQueue.Count > 0 && _readbackQueue.Peek().readBuffer[0] != 0)
        {
            var frame = _readbackQueue.Dequeue();
            _freeFrameQueue.Enqueue(frame);
        }

        if (_freeFrameQueue.Count > 0)
            _exposedData = new NativeSlice<int>(_freeFrameQueue.Peek().readBuffer, 1);

        if (_freeFrameQueue.Count == 0)
        {
            var frame = new Frame();
            frame.Allocate(_sourceBuffer);
            Graphics.ExecuteCommandBuffer(frame.copyCommand);
            _readbackQueue.Enqueue(frame);
        }
        else
        {
            var frame = _freeFrameQueue.Dequeue();
            Graphics.ExecuteCommandBuffer(frame.copyCommand);
            frame.readBuffer[0] = 0;
            _readbackQueue.Enqueue(frame);
        }
    }

    #endregion

    #region Frame read buffer

    struct Frame
    {
        public NativeArray<int> readBuffer;
        public NativeArray<CopyBufferEventArgs> copyArgs;
        public CommandBuffer copyCommand;

        unsafe public void Allocate(ComputeBuffer source)
        {
            readBuffer = new NativeArray<int>(source.count + 1, Allocator.Persistent);

            copyArgs = new NativeArray<CopyBufferEventArgs>(1, Allocator.Persistent);
            copyArgs[0] = new CopyBufferEventArgs {
                source = source.GetNativeBufferPtr(),
                destination = (IntPtr)readBuffer.GetUnsafePtr(),
                lengthInBytes = source.count * 4
            };

            copyCommand = new CommandBuffer();
            copyCommand.IssuePluginEventAndData(
                BufferAccessor_GetCopyBufferCallback(),
                0, (IntPtr)copyArgs.GetUnsafePtr()
            );
        }

        public void Deallocate()
        {
            readBuffer.Dispose();
            copyArgs.Dispose();
            copyCommand.Dispose();
            copyCommand = null;
        }
    }

    Queue<Frame> _readbackQueue = new Queue<Frame>();
    Queue<Frame> _freeFrameQueue = new Queue<Frame>();

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
            while (_readbackQueue.Count > 0) _readbackQueue.Dequeue().Deallocate();
            while (_freeFrameQueue.Count > 0) _freeFrameQueue.Dequeue().Deallocate();
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
    const string _dllName = "BufferAccessor";
    #endif

    [DllImport(_dllName)] static extern IntPtr BufferAccessor_GetCopyBufferCallback();

    #endregion
}
