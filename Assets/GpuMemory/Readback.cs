using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Klak.GpuMemory
{
    // Public accessor for readback request
    public struct ReadbackRequest
    {
        #region Public properties

        public bool HasError { get { return false; } }
        public bool IsCompleted { get { return _buffer.IsCompleted; } }
        public NativeSlice<int> Data { get { return _buffer.Data; } }

        #endregion

        #region Private members

        ReadbackBuffer _buffer;
        internal ReadbackRequest(ReadbackBuffer buffer) { _buffer = buffer; }

        #endregion
    }

    // Readback operation manager
    public static class ReadbackManager
    {
        #region public methods

        public static ReadbackRequest CreateRequest(ComputeBuffer source)
        {
            if (_sharedCommandBuffer == null) _sharedCommandBuffer = new CommandBuffer();
            var buffer = new ReadbackBuffer(source, _sharedCommandBuffer);
            _liveBuffers.Add(buffer);
            return new ReadbackRequest(buffer);
        }

        public static void Update()
        {
            var frame = Time.frameCount;
            if (_lastUpdateFrame == frame) return;

            foreach (var buffer in _completedBuffers) buffer.Dispose();
            _completedBuffers.Clear();

            foreach (var buffer in _liveBuffers)
                if (buffer.IsCompleted) _completedBuffers.Add(buffer);

            foreach (var buffer in _completedBuffers)
                _liveBuffers.Remove(buffer);

            _lastUpdateFrame = frame;
        }

        public static void Cleanup()
        {
            foreach (var buffer in _liveBuffers) buffer.Dispose();
            foreach (var buffer in _completedBuffers) buffer.Dispose();

            _liveBuffers.Clear();
            _completedBuffers.Clear();
        }

        #endregion

        #region Private members

        static List<ReadbackBuffer> _liveBuffers = new List<ReadbackBuffer>();
        static List<ReadbackBuffer> _completedBuffers = new List<ReadbackBuffer>();
        static CommandBuffer _sharedCommandBuffer;
        static int _lastUpdateFrame;

        #endregion
    }
}
