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

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_Create(int size);

    [DllImport("BufferAccessor")]
    static extern void BufferAccessor_Destroy(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetCopyBufferCallback();

    #endregion

    #region Source and destination buffers

    ComputeBuffer _gpuBuffer;
    int [] _managedBuffer;

    #endregion

    #region Shared command buffer for temporary use

    CommandBuffer _command;

    #endregion

    #region Readback queue

    struct Readback
    {
        public GCHandle pluginArgs;
        public IntPtr receiveBuffer;

        public void ReleaseResources()
        {
            pluginArgs.Free();
            BufferAccessor_Destroy(receiveBuffer);
        }
    }

    Queue<Readback> _readbackQueue;

    #endregion

    #region Readback thread

    Thread[] _readbackThreads;
    AutoResetEvent _readbackEvent;

    void ReadbackThreadFunction()
    {
        while (true)
        {
            _readbackEvent.WaitOne();

            Readback readback;
            lock (_readbackQueue) readback = _readbackQueue.Dequeue();

            var pointer = BufferAccessor_GetContents(readback.receiveBuffer);
            Marshal.Copy(pointer, _managedBuffer, 0, _bufferSize);

            readback.ReleaseResources();
        }
    }

    #endregion

    #region MonoBehaviour implementation

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(_bufferSize, 4);
        _managedBuffer = new int [_bufferSize];

        _command = new CommandBuffer();

        _readbackQueue = new Queue<Readback>();

        _readbackThreads = new Thread[5];
        _readbackEvent = new AutoResetEvent(false);

        for (var i = 0; i < _readbackThreads.Length; i++)
        {
            _readbackThreads[i] = new Thread(ReadbackThreadFunction);
            _readbackThreads[i].Start(i);
        }
    }

    void OnDisable()
    {
        foreach (var th in _readbackThreads) th.Abort();
        _readbackThreads = null;

        _gpuBuffer.Dispose();
        _gpuBuffer = null;

        _managedBuffer = null;

        _command.Dispose();
        _command = null;

        while (_readbackQueue.Count > 0)
            _readbackQueue.Dequeue().ReleaseResources();

        _readbackEvent = null;
    }

    void Update()
    {
        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        if (_readbackQueue.Count > _readbackThreads.Length - 1) return;

        var ibuffer = BufferAccessor_Create(_bufferSize * 4);

        var readback = new Readback {
            pluginArgs = GCHandle.Alloc(
                new CopyBufferArgs {
                    source = _gpuBuffer.GetNativeBufferPtr(),
                    destination = ibuffer,
                    length = _bufferSize * 4
                },
                GCHandleType.Pinned
            ),
            receiveBuffer = ibuffer
        };

        _command.Clear();
        _command.IssuePluginEventAndData(
            BufferAccessor_GetCopyBufferCallback(),
            0, readback.pluginArgs.AddrOfPinnedObject()
        );
        Graphics.ExecuteCommandBuffer(_command);

        lock (_readbackQueue) _readbackQueue.Enqueue(readback);
        _readbackEvent.Set();

        Debug.Log(_managedBuffer[_bufferSize - 1]);
    }

    #endregion
}
