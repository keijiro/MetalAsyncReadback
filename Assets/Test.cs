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
    const string _dllName = "__Internal";
    #else
    const string _dllName = "BufferAccessor";
    #endif

    [DllImport(_dllName)] static extern IntPtr BufferAccessor_Create(int size);
    [DllImport(_dllName)] static extern void BufferAccessor_Destroy(IntPtr buffer);
    [DllImport(_dllName)] static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);
    [DllImport(_dllName)] static extern IntPtr BufferAccessor_GetCopyBufferCallback();

    #endregion

    #region Source and destination buffers

    ComputeBuffer _gpuBuffer;

    #endregion

    #region Shared command buffer for temporary use

    CommandBuffer _command;

    #endregion

    #region Frame queue

    struct Frame
    {
        public GCHandle copyBufferArgs;
        public IntPtr copyBuffer;
        public int [] managedBuffer;

        public Frame(int bufferSize)
        {
            copyBufferArgs = new GCHandle();
            copyBuffer = BufferAccessor_Create(4 * bufferSize);
            managedBuffer = new int [bufferSize];
        }

        public void ReleaseResources()
        {
            if (copyBuffer != IntPtr.Zero) BufferAccessor_Destroy(copyBuffer);
            if (copyBufferArgs.IsAllocated) copyBufferArgs.Free();
            managedBuffer = null;
        }
    }

    Queue<Frame> _frameQueue = new Queue<Frame>();

    #endregion

    #region Readback thread

    Stack<Thread> _readbackThreads = new Stack<Thread>();
    AutoResetEvent _readbackEvent = new AutoResetEvent(false);
    IntPtr _readbackSource;
    int[] _readbackDestination;

    void ReadbackThreadFunction()
    {
        while (true)
        {
            _readbackEvent.WaitOne();
            var dest = _readbackDestination;
            Marshal.Copy(_readbackSource, dest, 0, _bufferSize);
            Debug.Log(String.Format("{0:X} : {1:X}", dest[1], dest[_bufferSize - 1]));
        }
    }

    #endregion

    #region Internal methods

    void SetupReadback()
    {
        while (_frameQueue.Count < 3)
            _frameQueue.Enqueue(new Frame(_bufferSize));

        while (_readbackThreads.Count < 3)
        {
            _readbackThreads.Push(new Thread(ReadbackThreadFunction));
            _readbackThreads.Peek().Start();
        }
    }

    void FinalizeReadback()
    {
        while (_readbackThreads.Count > 0)
            _readbackThreads.Pop().Abort();

        while (_frameQueue.Count > 0)
            _frameQueue.Dequeue().ReleaseResources();
    }

    void UpdateGpuBuffer()
    {
        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);
    }

    void QueueFrame()
    {
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
    }

    void KickReadback()
    {
        var frame = _frameQueue.Peek();
        _readbackSource = BufferAccessor_GetContents(frame.copyBuffer);
        _readbackDestination = frame.managedBuffer;
        _readbackEvent.Set();
    }

    #endregion

    #region MonoBehaviour implementation

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(_bufferSize, 4);
        _command = new CommandBuffer();
    }

    void OnDisable()
    {
        FinalizeReadback();

        _gpuBuffer.Dispose();
        _gpuBuffer = null;

        _command.Dispose();
        _command = null;
    }

    void Update()
    {
        SetupReadback();
        UpdateGpuBuffer();
        QueueFrame();
        KickReadback();
    }

    #endregion
}
