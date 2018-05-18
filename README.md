MetalAsyncReadback
------------------

This is an example that shows how to asynchronously read back GPU data into the
system memory in Unity running with the Metal graphics API mode (macOS/iOS).

This implementation is slightly different from the official implementation of
[AsyncGPUReadback]. You have to manually manage compute buffers to avoid
overwriting while readback.

The readback speed varies across systems; In general, MacBook and iOS devices
with an integrated GPU are faster than Mac systems with a discrete GPU.

[AsyncGPUReadback]: https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Experimental.Rendering.AsyncGPUReadback.html
