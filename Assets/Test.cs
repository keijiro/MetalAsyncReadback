using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Klak.GpuMemory;

public class Test : MonoBehaviour
{
    [SerializeField] int _bufferSize = 1920 * 1080;
    [SerializeField, HideInInspector] ComputeShader _compute;

    ComputeBuffer _source;
    Queue<ReadbackRequest> _queue = new Queue<ReadbackRequest>();

    void OnDisable()
    {
        _source.Dispose();
        _source = null;

        _queue.Clear();

        ReadbackManager.Cleanup();
    }

    void Update()
    {
        ReadbackManager.Update();

        while (_queue.Count > 0 && _queue.Peek().IsCompleted)
        {
            var data = _queue.Dequeue().Data;
            Debug.Log(string.Format("{0:X} : {1:X}", data[0], data[data.Length - 1]));
        }

        if (_source == null) _source = new ComputeBuffer(_bufferSize, 4);

        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.SetBuffer(0, "Destination", _source);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        _queue.Enqueue(ReadbackManager.CreateRequest(_source));

        if ((Time.frameCount & 0xf) == 0)
            Debug.Log("Queue length = " + _queue.Count);
    }
}
