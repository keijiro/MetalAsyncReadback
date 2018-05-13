using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class ReadbackBuffer : IDisposable
{
    #region Public methods

    ComputeBuffer _sourceBuffer;

    public ReadbackBuffer(ComputeBuffer source)
    {
        _sourceBuffer = source;
        _command = new CommandBuffer();
    }

    public void Update()
    {
        SetupQueue();
        QueueFrame();
        KickRetrieval();
    }

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
            FinalizeQueue();
            _command.Dispose();
            _command = null;
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

    [DllImport(_dllName)] static extern IntPtr BufferAccessor_Create(int size);
    [DllImport(_dllName)] static extern void BufferAccessor_Destroy(IntPtr buffer);
    [DllImport(_dllName)] static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);
    [DllImport(_dllName)] static extern IntPtr BufferAccessor_GetCopyBufferCallback();

    #endregion

    #region Shared command buffer for temporary use

    CommandBuffer _command;

    #endregion

    #region Readback frame queue

    struct Frame
    {
        public GCHandle copyArgs;
        public IntPtr copyBuffer;
        public int [] readBuffer;

        public Frame(int bufferSize)
        {
            copyArgs = new GCHandle();
            copyBuffer = BufferAccessor_Create(4 * bufferSize);
            readBuffer = new int [bufferSize];
        }

        public void ReleaseResources()
        {
            if (copyBuffer != IntPtr.Zero) BufferAccessor_Destroy(copyBuffer);
            if (copyArgs.IsAllocated) copyArgs.Free();
            readBuffer = null;
        }
    }

    Queue<Frame> _copyFrameQueue = new Queue<Frame>();
    Queue<Frame> _retrievalQueue = new Queue<Frame>();

    #endregion

    #region Frame retrieval thread

    struct RetrievalArgs
    {
        public IntPtr source;
        public int[] destination;
        public int count;
    }

    Stack<Thread> _retrievalThreads = new Stack<Thread>();
    AutoResetEvent _retrievalEvent = new AutoResetEvent(false);
    RetrievalArgs _retrievalArgs;

    void RetrievalThreadFunction()
    {
        while (true)
        {
            _retrievalEvent.WaitOne();

            var args = _retrievalArgs;
            Marshal.Copy(args.source, args.destination, 0, args.count);

            Debug.Log(String.Format(
                "{0:X} : {1:X}",
                args.destination[0],
                args.destination[args.count - 1]
            ));
        }
    }

    #endregion

    #region Internal methods

    void SetupQueue()
    {
        while (_copyFrameQueue.Count < 2)
            _copyFrameQueue.Enqueue(new Frame(_sourceBuffer.count));

        while (_retrievalQueue.Count < 2)
            _retrievalQueue.Enqueue(new Frame(_sourceBuffer.count));

        while (_retrievalThreads.Count < 2)
        {
            _retrievalThreads.Push(new Thread(RetrievalThreadFunction));
            _retrievalThreads.Peek().Start();
        }
    }

    void FinalizeQueue()
    {
        while (_retrievalThreads.Count > 0)
            _retrievalThreads.Pop().Abort();

        while (_copyFrameQueue.Count > 0)
            _copyFrameQueue.Dequeue().ReleaseResources();

        while (_retrievalQueue.Count > 0)
            _retrievalQueue.Dequeue().ReleaseResources();
    }

    void QueueFrame()
    {
        var frame = _retrievalQueue.Dequeue();

        frame.copyArgs = GCHandle.Alloc(
            new CopyBufferEventArgs {
                source = _sourceBuffer.GetNativeBufferPtr(),
                destination = frame.copyBuffer,
                lengthInBytes = _sourceBuffer.count * 4
            },
            GCHandleType.Pinned
        );

        _command.Clear();
        _command.IssuePluginEventAndData(
            BufferAccessor_GetCopyBufferCallback(),
            0, frame.copyArgs.AddrOfPinnedObject()
        );
        Graphics.ExecuteCommandBuffer(_command);

        _copyFrameQueue.Enqueue(frame);
    }

    void KickRetrieval()
    {
        var frame = _copyFrameQueue.Dequeue();
        _retrievalArgs = new RetrievalArgs {
            source = BufferAccessor_GetContents(frame.copyBuffer),
            destination = frame.readBuffer,
            count = _sourceBuffer.count
        };
        _retrievalEvent.Set();
        _retrievalQueue.Enqueue(frame);
    }

    #endregion
}
