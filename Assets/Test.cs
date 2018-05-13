using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] int _bufferSize = 1920 * 1080;

    [SerializeField, HideInInspector] ComputeShader _compute;

    ComputeBuffer _gpuBuffer;
    ReadbackBuffer _readback;

    void OnEnable()
    {
        _gpuBuffer = new ComputeBuffer(_bufferSize, 4);
        _readback = new ReadbackBuffer(_gpuBuffer);
    }

    void OnDisable()
    {
        _readback.Dispose();
        _gpuBuffer.Dispose();
    }

    void Update()
    {
        _compute.SetBuffer(0, "Destination", _gpuBuffer);
        _compute.SetInt("FrameCount", Time.frameCount);
        _compute.Dispatch(0, _bufferSize / 64, 1, 1);

        var na = _readback.nativeArray;
        if (na.IsCreated)
            Debug.Log(string.Format("{0:X} : {1:X}", na[0], na[na.Length - 1]));

        _readback.Update();
    }
}
