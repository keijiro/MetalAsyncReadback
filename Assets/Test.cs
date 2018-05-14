using UnityEngine;
using System.Collections.Generic;

public class Test : MonoBehaviour
{
    [SerializeField] int _bufferSize = 1920 * 1080;

    [SerializeField, HideInInspector] ComputeShader _compute;

    Queue<ComputeBuffer> _gpuBufferQueue = new Queue<ComputeBuffer>();
    ReadbackBuffer _readback;

    void OnEnable()
    {
        for (var i = 0; i < 4; i++)
            _gpuBufferQueue.Enqueue(new ComputeBuffer(_bufferSize, 4));

        _readback = new ReadbackBuffer(_gpuBufferQueue.Peek());
    }

    void OnDisable()
    {
        _readback.Dispose();

        while (_gpuBufferQueue.Count > 0)
            _gpuBufferQueue.Dequeue().Dispose();
    }

    void Update()
    {
        var gpuBuffer = _gpuBufferQueue.Dequeue();
        _readback.sourceBuffer = gpuBuffer;

        _compute.SetBuffer(0, "Destination", gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        _readback.Update();

        var slice = _readback.data;
        if (slice.Length > 0)
            Debug.Log(string.Format("{0:X} : {1:X}", slice[0], slice[slice.Length - 1]));

        _gpuBufferQueue.Enqueue(gpuBuffer);
    }
}
