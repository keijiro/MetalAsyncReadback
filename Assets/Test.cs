using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public class Test : MonoBehaviour
{
    enum Mode { CopyBuffer, SharedBuffer, GetData }

    [SerializeField] Mode _mode;
    [SerializeField] int _bufferSize = 1920 * 1080;

    [SerializeField, HideInInspector] ComputeShader _compute;

    struct CopyBufferArgs
    {
        public IntPtr source;
        public IntPtr destination;
        public int length;
    };

    ComputeBuffer _gpuBuffer;
    IntPtr _copyBuffer;
    int [] _managedBuffer;

    CommandBuffer _command;
    GCHandle _pluginArgs;

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(_bufferSize, 4);
        _copyBuffer = BufferAccessor_Create(_bufferSize * 4);
        _managedBuffer = new int [_bufferSize];
        _command = new CommandBuffer();
    }

    void OnDisable()
    {
        _gpuBuffer.Dispose();
        _gpuBuffer = null;

        BufferAccessor_Destroy(_copyBuffer);
        _copyBuffer = IntPtr.Zero;

        _managedBuffer = null;

        _command.Dispose();
        _command = null;

        if (_pluginArgs.IsAllocated) _pluginArgs.Free();
    }

    void Update()
    {
        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        if (_mode == Mode.CopyBuffer)
        {
            if (!_pluginArgs.IsAllocated)
            {
                _pluginArgs = GCHandle.Alloc(
                    new CopyBufferArgs {
                        source = _gpuBuffer.GetNativeBufferPtr(),
                        destination = _copyBuffer,
                        length = _bufferSize * 4
                    },
                    GCHandleType.Pinned
                );
            }

            _command.Clear();
            _command.IssuePluginEventAndData(
                BufferAccessor_GetCopyBufferCallback(),
                0, _pluginArgs.AddrOfPinnedObject()
            );
            Graphics.ExecuteCommandBuffer(_command);

            var pointer = BufferAccessor_GetContents(_copyBuffer);
            Marshal.Copy(pointer, _managedBuffer, 0, _bufferSize);
        }

        if (_mode == Mode.SharedBuffer)
        {
            var pointer = BufferAccessor_GetContents(_gpuBuffer.GetNativeBufferPtr());
            Marshal.Copy(pointer, _managedBuffer, 0, _bufferSize);
        }

        if (_mode == Mode.GetData)
            _gpuBuffer.GetData(_managedBuffer);

        Debug.Log(_managedBuffer[_bufferSize - 1]);
    }

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_Create(int size);

    [DllImport("BufferAccessor")]
    static extern void BufferAccessor_Destroy(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetContents(IntPtr buffer);

    [DllImport("BufferAccessor")]
    static extern IntPtr BufferAccessor_GetCopyBufferCallback();
}
