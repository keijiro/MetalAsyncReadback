using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class Test : MonoBehaviour
{
    [SerializeField] ComputeShader _compute;

    const int bufferSize = 1920 * 1080;

    struct CopyBufferArgs
    {
        public IntPtr source;
        public IntPtr destination;
        public uint length;
    };

    ComputeBuffer _gpuBuffer;
    CommandBuffer _command;
    GCHandle _args;

    IntPtr _copyBuffer;
    int [] _managedBuffer;

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(bufferSize, 4);
        _command = new CommandBuffer();

        _copyBuffer = BufferAccessor_Create(bufferSize * 4);

        _managedBuffer = new int [bufferSize];
    }

    void OnDisable()
    {
        _gpuBuffer.Dispose();
        _command.Dispose();

        if (_args.IsAllocated) _args.Free();

        BufferAccessor_Destroy(_copyBuffer);
        _copyBuffer = IntPtr.Zero;

        _managedBuffer = null;
    }

    void Update()
    {
        if (!_args.IsAllocated)
        {
            _args = GCHandle.Alloc(
                new CopyBufferArgs {
                    source = _gpuBuffer.GetNativeBufferPtr(),
                    destination = _copyBuffer,
                    length = bufferSize * 4
                },
                GCHandleType.Pinned
            );
        }

        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, bufferSize / 64, 1, 1);

        _command.Clear();
        _command.IssuePluginEventAndData(
            BufferAccessor_GetCopyBufferCallback(),
            0, _args.AddrOfPinnedObject()
        );

        Graphics.ExecuteCommandBuffer(_command);

        var pointer = BufferAccessor_GetContents(_copyBuffer);
        Marshal.Copy(pointer, _managedBuffer, 0, bufferSize);
        Debug.Log(_managedBuffer[bufferSize - 1]);
    }

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_Create(uint size);

    [DllImport("BufferAccessor")]
    static extern void BufferAccessor_Destroy(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetCopyBufferCallback();
}
