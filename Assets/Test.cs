using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class Test : MonoBehaviour
{
    #region Editable properties

    [SerializeField] int _bufferSize = 1920 * 1080;

    #endregion

    #region Internal resources

    [SerializeField, HideInInspector] ComputeShader _compute;

    #endregion

    #region Native plugin interface

    struct CopyBufferArgs
    {
        public IntPtr source;
        public IntPtr destination;
        public int length;
    }

    #if !UNITY_EDITOR && UNITY_IOS
    const string dllName = "__Internal";
    #else
    const string dllName = "BufferAccessor";
    #endif

    [DllImport(dllName)] static extern IntPtr BufferAccessor_Create(int size);
    [DllImport(dllName)] static extern void BufferAccessor_Destroy(IntPtr buffer);
    [DllImport(dllName)] static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);
    [DllImport(dllName)] static extern IntPtr BufferAccessor_GetCopyBufferCallback();

    #endregion

    #region Source and destination buffers

    ComputeBuffer _gpuBuffer;
    int [] _managedBuffer;

    #endregion

    #region Shared command buffer for temporary use

    CommandBuffer _command;

    #endregion

    #region Frame queue

    struct Frame
    {
        public IntPtr copyBuffer;
        public GCHandle copyBufferArgs;

        public void ReleaseResources()
        {
            if (copyBufferArgs.IsAllocated) copyBufferArgs.Free();
            if (copyBuffer != IntPtr.Zero) BufferAccessor_Destroy(copyBuffer);
        }
    }

    Queue<Frame> _frameQueue = new Queue<Frame>();

    #endregion

    #region Readback thread

    Stack<Thread> _readbackThreads = new Stack<Thread>();
    AutoResetEvent _readbackEvent = new AutoResetEvent(false);
    IntPtr _readbackSource;

    void ReadbackThreadFunction()
    {
        while (true)
        {
            _readbackEvent.WaitOne();
            Marshal.Copy(_readbackSource, _managedBuffer, 0, _bufferSize);
        }
    }

    #endregion

    #region MonoBehaviour implementation

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(_bufferSize, 4);
        _managedBuffer = new int [_bufferSize];
        _command = new CommandBuffer();
    }

    void OnDisable()
    {
        while (_readbackThreads.Count > 0)
            _readbackThreads.Pop().Abort();

        while (_frameQueue.Count > 0)
            _frameQueue.Dequeue().ReleaseResources();

        _gpuBuffer.Dispose();
        _gpuBuffer = null;

        _managedBuffer = null;

        _command.Dispose();
        _command = null;
    }

    void Update()
    {
        while (_frameQueue.Count < 3)
        {
            _frameQueue.Enqueue(new Frame{
                copyBuffer = BufferAccessor_Create(_bufferSize * 4)
            });
        }

        while (_readbackThreads.Count < 4)
        {
            _readbackThreads.Push(new Thread(ReadbackThreadFunction));
            _readbackThreads.Peek().Start();
        }

        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        var frame = _frameQueue.Dequeue();

        frame.copyBufferArgs = GCHandle.Alloc(
            new CopyBufferArgs {
                source = _gpuBuffer.GetNativeBufferPtr(),
                destination = frame.copyBuffer,
                length = _bufferSize * 4
            },
            GCHandleType.Pinned
        );

        _command.Clear();
        _command.IssuePluginEventAndData(
            BufferAccessor_GetCopyBufferCallback(),
            0, frame.copyBufferArgs.AddrOfPinnedObject()
        );
        Graphics.ExecuteCommandBuffer(_command);

        _frameQueue.Enqueue(frame);

        frame = _frameQueue.Peek();
        _readbackSource = BufferAccessor_GetContents(frame.copyBuffer);
        _readbackEvent.Set();

        Debug.Log(_managedBuffer[_bufferSize - 1]);
    }

    #endregion
}
