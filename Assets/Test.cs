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

    #region Command buffer

    CommandBuffer _command;

    #endregion

    #region Readback queue

    struct Readback
    {
        public GCHandle args;
        public IntPtr buffer;

        public void ReleaseResources()
        {
            args.Free();
            BufferAccessor_Destroy(buffer);
            BufferAccessor_Destroy(buffer);
        }
    }

    Queue<Readback> _readbackQueue;

    #endregion

    #region Readback thread

    Thread[] _readbackThreads;
    AutoResetEvent _readbackRequest;
    bool _readbackThreadStop;

    void ReadbackThreadFunction()
    {
        while (!_readbackThreadStop)
        {
            _readbackRequest.WaitOne();

            if (_readbackThreadStop) break;

            Readback readback;
            lock (_readbackQueue) readback = _readbackQueue.Dequeue();

            var pointer = BufferAccessor_GetContents(readback.buffer);
            Debug.Log(">>" + pointer);
            Marshal.Copy(pointer, _managedBuffer, 0, _bufferSize);
            Debug.Log("<<" + pointer);

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

        _readbackThreads = new Thread[6];
        _readbackRequest = new AutoResetEvent(false);
        _readbackThreadStop = false;

        for (var i = 0; i < _readbackThreads.Length; i++)
        {
            _readbackThreads[i] = new Thread(ReadbackThreadFunction);
            _readbackThreads[i].Start(i);
        }
    }

    void OnDisable()
    {
        //_readbackThreadStop = true;
        //_readbackRequest.Close();

        foreach (var th in _readbackThreads)
        th.Abort();
            //if (th.ThreadState != ThreadState.Unstarted) th.Join();

        _readbackThreads = null;

        _gpuBuffer.Dispose();
        _gpuBuffer = null;

        _managedBuffer = null;

        _command.Dispose();
        _command = null;

        while (_readbackQueue.Count > 0)
            _readbackQueue.Dequeue().ReleaseResources();

        _readbackRequest = null;
    }

    void Update()
    {
        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        if (_readbackQueue.Count > _readbackThreads.Length - 1) return;

        var ibuffer = BufferAccessor_Create(_bufferSize * 4);

        var readback = new Readback {
            args = GCHandle.Alloc(
                new CopyBufferArgs {
                    source = _gpuBuffer.GetNativeBufferPtr(),
                    destination = ibuffer,
                    length = _bufferSize * 4
                },
                GCHandleType.Pinned
            ),
            buffer = ibuffer
        };

        _command.Clear();
        _command.IssuePluginEventAndData(
            BufferAccessor_GetCopyBufferCallback(),
            0, readback.args.AddrOfPinnedObject()
        );
        Graphics.ExecuteCommandBuffer(_command);

        lock (_readbackQueue) _readbackQueue.Enqueue(readback);
        _readbackRequest.Set();

        Debug.Log(_managedBuffer[_bufferSize - 1]);
    }

    #endregion
}
