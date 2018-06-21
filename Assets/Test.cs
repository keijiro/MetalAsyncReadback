using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using MetalAsyncReadback;

public class Test : MonoBehaviour
{
    [SerializeField] int _bufferSize = 1920 * 1080;

    [SerializeField, HideInInspector] ComputeShader _compute;

    Queue<ReadbackBuffer> _bufferQueue = new Queue<ReadbackBuffer>();

    void CheckData(NativeSlice<int> data)
    {
        Debug.Log(string.Format("{0:X} : {1:X}", data[0], data[data.Length - 1]));
    }

    void DisposeBuffer(ReadbackBuffer buffer)
    {
        buffer.Source.Dispose();
        buffer.Dispose();
    }

    void OnDisable()
    {
        while (_bufferQueue.Count > 0) DisposeBuffer(_bufferQueue.Dequeue());
    }

    void Update()
    {
        var canDequeue = (_bufferQueue.Count > 0 && _bufferQueue.Peek().IsCompleted);

        if (canDequeue) CheckData(_bufferQueue.Peek().Data);

        var buffer = canDequeue ? _bufferQueue.Dequeue() : 
            new ReadbackBuffer(new ComputeBuffer(_bufferSize, 4));

        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.SetBuffer(0, "Destination", buffer.Source);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);
        buffer.RequestReadback();

        _bufferQueue.Enqueue(buffer);

        if ((Time.frameCount & 0xf) == 0)
            Debug.Log("Queue length = " + _bufferQueue.Count);
    }
}
