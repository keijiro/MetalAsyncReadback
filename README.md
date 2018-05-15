MetalBufferAccessor
-------------------

This is an example that shows how to asynchronously read back GPU data into CPU
(system) memory in Unity running on the Metal graphics API mode (macOS/iOS).

This implementation is slightly different from the official implementation of
[AsyncGPUReadback]. You have to manually manage compute buffers to avoid
overwriting while readback.

[AsyncGPUReadback]: https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Experimental.Rendering.AsyncGPUReadback.html
