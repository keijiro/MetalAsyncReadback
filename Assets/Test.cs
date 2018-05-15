using UnityEngine;
using System.Collections.Generic;

public class Test : MonoBehaviour
{
    [SerializeField] int _bufferSize = 1920 * 1080;

    [SerializeField, HideInInspector] ComputeShader _compute;

    struct BufferPair
    {
        public ComputeBuffer compute;
        public ReadbackBuffer readback;

        public void Dispose()
        {
            compute.Dispose();
            readback.Dispose();
        }
    }

    Queue<BufferPair> _bufferQueue = new Queue<BufferPair>();

    void OnDisable()
    {
        while (_bufferQueue.Count > 0) _bufferQueue.Dequeue().Dispose();
    }

    void Update()
    {
        _compute.SetInt("FrameCount", Time.frameCount);

        if (_bufferQueue.Count > 0 && _bufferQueue.Peek().readback.IsCompleted)
        {
            var pair = _bufferQueue.Dequeue();

            var data = pair.readback.Data;
            Debug.Log(string.Format(
                "{0:X} : {1:X}", data[0], data[data.Length - 1]
            ));

            _compute.SetBuffer(0, "Destination", pair.compute);
            _compute.Dispatch(0, _bufferSize / 64, 1, 1);
            pair.readback.RequestReadback();

            _bufferQueue.Enqueue(pair);
        }
        else
        {
            var cb = new ComputeBuffer(_bufferSize, 4);

            var pair = new BufferPair {
                compute = cb, readback = new ReadbackBuffer(cb)
            };

            _compute.SetBuffer(0, "Destination", cb);
            _compute.Dispatch(0, _bufferSize / 64, 1, 1);
            pair.readback.RequestReadback();

            _bufferQueue.Enqueue(pair);
        }
    }
}
